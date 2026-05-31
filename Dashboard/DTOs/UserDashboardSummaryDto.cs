namespace VoxTrade.Dashboard.DTOs;

public class UserDashboardSummaryDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstNameEn { get; set; } = string.Empty;
    public string LastNameEn { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public bool IsDeleted { get; set; }
    public UserDashboardWalletDto? Wallet { get; set; }
    public UserDashboardOrdersSummaryDto Orders { get; set; } = new();
    public UserDashboardPositionsSummaryDto Positions { get; set; } = new();
    public decimal TotalPortfolioValue { get; set; }
    public int TotalTrades { get; set; }
    public int TradesLast7Days { get; set; }
    public List<UserRecentActivityItemDto> RecentActivity { get; set; } = new();
}
