namespace VoxTrade.Dashboard.DTOs;

public class UserDashboardOrdersSummaryDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int PartiallyFilled { get; set; }
    public int Filled { get; set; }
    public int Cancelled { get; set; }
    public int Active { get; set; }
}
