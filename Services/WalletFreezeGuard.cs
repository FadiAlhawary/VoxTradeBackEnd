using System.Data;
using Dapper;

namespace VoxTrade.Services;

/// <summary>
/// Shared checks for wallets with status = false (frozen).
/// </summary>
public static class WalletFreezeGuard
{
    public static Task<bool> IsUserWalletFrozenAsync(IDbConnection connection, int userId) =>
        IsUserWalletFrozenAsync(connection, userId, transaction: null);

    public static async Task<bool> IsUserWalletFrozenAsync(
        IDbConnection connection,
        int userId,
        IDbTransaction? transaction)
    {
        var status = await connection.ExecuteScalarAsync<bool?>(
            """
            SELECT status
            FROM public.wallets
            WHERE user_id = @UserId
            LIMIT 1;
            """,
            new { UserId = userId },
            transaction);

        return status == false;
    }

    public static Task<string?> GetFrozenMessageIfAnyAsync(IDbConnection connection, int userId) =>
        GetFrozenMessageIfAnyAsync(connection, userId, transaction: null);

    public static async Task<string?> GetFrozenMessageIfAnyAsync(
        IDbConnection connection,
        int userId,
        IDbTransaction? transaction)
    {
        if (!await IsUserWalletFrozenAsync(connection, userId, transaction))
            return null;

        var reason = await connection.ExecuteScalarAsync<string?>(
            """
            SELECT freeze_reason
            FROM public.wallets
            WHERE user_id = @UserId
            LIMIT 1;
            """,
            new { UserId = userId },
            transaction);

        return string.IsNullOrWhiteSpace(reason)
            ? "Wallet is frozen. Trading and fund movements are disabled."
            : $"Wallet is frozen: {reason.Trim()}";
    }
}
