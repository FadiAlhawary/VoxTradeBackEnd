using VoxTrade.Dashboard.DTOs;
using VoxTrade.Models.DTO;

namespace VoxTrade.Dashboard.Interfaces;

public interface IUserDashboardService
{
    Task<UserDashboardSummaryDto?> GetSummaryAsync(int userId, int recentActivityLimit = 10);
    Task<UserDashboardWalletDto?> GetWalletSummaryAsync(int userId);
    Task<UserDashboardOrdersSummaryDto> GetOrdersSummaryAsync(int userId);
    Task<List<PortfolioPositionDto>> GetPositionsAsync(int userId);
    Task<List<UserRecentActivityItemDto>> GetRecentActivityAsync(int userId, int limit = 20);
    Task<List<MarketInstrumentSnapshotDto>> GetMarketSnapshotAsync(int limit = 20);
    Task<List<UserTransferRecipientDto>> SearchTransferRecipientsAsync(string query, int limit = 20, int? excludeUserId = null);
}
