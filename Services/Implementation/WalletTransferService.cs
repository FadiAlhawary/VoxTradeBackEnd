using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.Models.DTO;
using VoxTrade.Services;
using VoxTrade.Services.Interface;

namespace VoxTrade.Services.Implementation;

/// <summary>
/// Peer-to-peer wallet transfers between users (Dapper + DB transaction).
/// </summary>
public class WalletTransferService : IWalletTransferService
{
    private readonly TradingDbContext _context;
    private readonly ILogger<WalletTransferService> _logger;

    public WalletTransferService(TradingDbContext context, ILogger<WalletTransferService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TransferMoneyResponseDto> TransferMoneyAsync(TransferMoneyRequestDto request)
    {
        if (request.FromUserId <= 0 || request.ToUserId <= 0)
        {
            return Fail("Valid sender and recipient user ids are required.");
        }

        if (request.FromUserId == request.ToUserId)
        {
            return Fail("Cannot transfer money to yourself.");
        }

        if (request.Amount <= 0)
        {
            return Fail("Amount must be greater than zero.");
        }

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var senderFrozen = await WalletFreezeGuard.GetFrozenMessageIfAnyAsync(
                connection, request.FromUserId, transaction);
            if (senderFrozen != null)
                return Fail(senderFrozen);

            var receiverFrozen = await WalletFreezeGuard.GetFrozenMessageIfAnyAsync(
                connection, request.ToUserId, transaction);
            if (receiverFrozen != null)
                return Fail("Recipient wallet is frozen and cannot receive transfers.");

            var senderActive = await IsActiveUserAsync(connection, request.FromUserId, transaction);
            if (!senderActive)
                return Fail("Sender account not found or is not active.");

            var receiverActive = await IsActiveUserAsync(connection, request.ToUserId, transaction);
            if (!receiverActive)
                return Fail("Recipient account not found or is not active.");

            var senderWallet = await LockWalletAsync(connection, request.FromUserId, transaction);
            if (senderWallet == null)
                return Fail("Sender wallet not found.");

            var receiverWallet = await LockWalletAsync(connection, request.ToUserId, transaction);
            if (receiverWallet == null)
                return Fail("Recipient wallet not found.");

            if (senderWallet.AvailableBalance < request.Amount)
            {
                return Fail(
                    $"Insufficient available balance. Available: {senderWallet.AvailableBalance:N2}, required: {request.Amount:N2}.");
            }

            var senderUsername = await GetUsernameAsync(connection, request.FromUserId, transaction);
            var receiverUsername = await GetUsernameAsync(connection, request.ToUserId, transaction);

            var note = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
            var senderDescription = note ?? $"Transfer to {receiverUsername ?? $"user {request.ToUserId}"}";
            var receiverDescription = note ?? $"Transfer from {senderUsername ?? $"user {request.FromUserId}"}";

            var senderBalanceAfter = senderWallet.Balance - request.Amount;
            var senderAvailableAfter = senderWallet.AvailableBalance - request.Amount;
            var receiverBalanceAfter = receiverWallet.Balance + request.Amount;
            var receiverAvailableAfter = receiverWallet.AvailableBalance + request.Amount;

            await connection.ExecuteAsync(
                """
                UPDATE public.wallets
                SET balance = @Balance,
                    available_balance = @AvailableBalance,
                    updated_at = NOW()
                WHERE id = @WalletId;
                """,
                new
                {
                    WalletId = senderWallet.Id,
                    Balance = senderBalanceAfter,
                    AvailableBalance = senderAvailableAfter
                },
                transaction);

            await connection.ExecuteAsync(
                """
                UPDATE public.wallets
                SET balance = @Balance,
                    available_balance = @AvailableBalance,
                    updated_at = NOW()
                WHERE id = @WalletId;
                """,
                new
                {
                    WalletId = receiverWallet.Id,
                    Balance = receiverBalanceAfter,
                    AvailableBalance = receiverAvailableAfter
                },
                transaction);

            await InsertWalletHistoryAsync(
                connection,
                transaction,
                senderWallet,
                request.FromUserId,
                "transfer_out",
                request.Amount,
                senderBalanceAfter,
                senderAvailableAfter,
                senderDescription);

            await InsertWalletHistoryAsync(
                connection,
                transaction,
                receiverWallet,
                request.ToUserId,
                "transfer_in",
                request.Amount,
                receiverBalanceAfter,
                receiverAvailableAfter,
                receiverDescription);

            await transaction.CommitAsync();

            return new TransferMoneyResponseDto
            {
                Success = true,
                Message = "Transfer completed successfully.",
                FromUserId = request.FromUserId,
                ToUserId = request.ToUserId,
                Amount = request.Amount,
                SenderBalanceAfter = senderBalanceAfter,
                SenderAvailableBalanceAfter = senderAvailableAfter,
                ReceiverBalanceAfter = receiverBalanceAfter,
                ReceiverAvailableBalanceAfter = receiverAvailableAfter
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(
                ex,
                "P2P transfer failed from {FromUserId} to {ToUserId}, amount {Amount}",
                request.FromUserId,
                request.ToUserId,
                request.Amount);
            return Fail("Transfer failed. Please try again later.");
        }
    }

    private static TransferMoneyResponseDto Fail(string message) =>
        new() { Success = false, Message = message };

    private static async Task<bool> IsActiveUserAsync(
        IDbConnection connection,
        int userId,
        IDbTransaction transaction)
    {
        var count = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM public.users
            WHERE id = @UserId
              AND COALESCE(is_deleted, false) = false
              AND COALESCE(is_locked, false) = false;
            """,
            new { UserId = userId },
            transaction);

        return count > 0;
    }

    private static async Task<string?> GetUsernameAsync(
        IDbConnection connection,
        int userId,
        IDbTransaction transaction)
    {
        return await connection.ExecuteScalarAsync<string?>(
            "SELECT username FROM public.users WHERE id = @UserId;",
            new { UserId = userId },
            transaction);
    }

    private sealed class WalletRow
    {
        public int Id { get; init; }
        public int UserId { get; init; }
        public decimal Balance { get; init; }
        public decimal AvailableBalance { get; init; }
        public decimal ReservedBalance { get; init; }
    }

    private static async Task<WalletRow?> LockWalletAsync(
        IDbConnection connection,
        int userId,
        IDbTransaction transaction)
    {
        return await connection.QueryFirstOrDefaultAsync<WalletRow>(
            """
            SELECT
                id AS Id,
                user_id AS UserId,
                balance AS Balance,
                available_balance AS AvailableBalance,
                reserved_balance AS ReservedBalance
            FROM public.wallets
            WHERE user_id = @UserId
            FOR UPDATE;
            """,
            new { UserId = userId },
            transaction);
    }

    private static async Task InsertWalletHistoryAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        WalletRow wallet,
        int userId,
        string transactionType,
        decimal amount,
        decimal balanceAfter,
        decimal availableAfter,
        string description)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO public.wallet_history (
                wallet_id,
                user_id,
                transaction_type,
                amount,
                balance_before,
                balance_after,
                available_before,
                available_after,
                reserved_before,
                reserved_after,
                description,
                created_at
            )
            VALUES (
                @WalletId,
                @UserId,
                @TransactionType,
                @Amount,
                @BalanceBefore,
                @BalanceAfter,
                @AvailableBefore,
                @AvailableAfter,
                @ReservedBefore,
                @ReservedAfter,
                @Description,
                NOW()
            );
            """,
            new
            {
                WalletId = wallet.Id,
                UserId = userId,
                TransactionType = transactionType,
                Amount = amount,
                BalanceBefore = wallet.Balance,
                BalanceAfter = balanceAfter,
                AvailableBefore = wallet.AvailableBalance,
                AvailableAfter = availableAfter,
                ReservedBefore = wallet.ReservedBalance,
                ReservedAfter = wallet.ReservedBalance,
                Description = description
            },
            transaction);
    }
}
