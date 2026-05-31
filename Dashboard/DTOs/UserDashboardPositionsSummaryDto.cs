namespace VoxTrade.Dashboard.DTOs;

public class UserDashboardPositionsSummaryDto
{
    public int PositionCount { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalMarketValue { get; set; }
    public decimal TotalUnrealizedPnl { get; set; }
    public decimal TotalRealizedPnl { get; set; }
}
