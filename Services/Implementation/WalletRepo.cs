using Dapper;
using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.Models;
using VoxTrade.Models.DTO;
using VoxTrade.Services.Interface;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace VoxTrade.Services.Implementation
{
    public class WalletRepo : IWalletRepo
    {
        private readonly TradingDbContext _context;
        private readonly ILogger<WalletRepo> _logger;
        private readonly IHistoryRepository _historyRepository;
        public WalletRepo(TradingDbContext context, ILogger<WalletRepo> logger, IHistoryRepository historyRepository)
        {
            _context = context;
            _logger = logger;
            _historyRepository = historyRepository;
        }
        public async Task<WalletDto> GetWallet(int userId, bool withHistory)
        {
            try
            {
                const string sql =
                """
                select 
                    id,
                    user_id as UserId,
                    currency_id as CurrencyId,
                    balance,
                    available_balance as AvailableBalance,
                    reserved_balance as ReservedBalance,
                    status,
                    updated_at as UpdatedAt
                from wallets
                where user_id = @userId
                """;

                var connection = _context.Database.GetDbConnection();

                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                var result = await connection.QueryFirstOrDefaultAsync<Wallet>(
                    sql,
                    new { userId });

                if (result == null)
                {
                    return new WalletDto
                    {
                        UserId = userId,
                        Balance = 0,
                        AvailableBalance = 0,
                        ReservedBalance = 0,
                        walletHistory = withHistory
                            ? await _historyRepository.GetWalletHistory(userId)
                              ?? new List<WalletHistoryDto>()
                            : new List<WalletHistoryDto>()
                    };
                }

                var wallet = new WalletDto
                {
                    Id = result.Id,
                    UserId = result.UserId,
                    CurrencyId = result.CurrencyId,
                    Balance = result.Balance,
                    AvailableBalance = result.AvailableBalance,
                    ReservedBalance = result.ReservedBalance,
                    Status = result.Status,
                    UpdatedAt = result.UpdatedAt,
                    walletHistory = new List<WalletHistoryDto>()
                };

                if (withHistory)
                {
                    wallet.walletHistory =
                        await _historyRepository.GetWalletHistory(userId)
                        ?? new List<WalletHistoryDto>();
                }

                return wallet;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get wallet for user id {UserId}", userId);
                throw;
            }
        }
    }
}
