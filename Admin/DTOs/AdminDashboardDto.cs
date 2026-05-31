namespace VoxTrade.Admin.DTOs;

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int LockedUsers { get; set; }
    public int TotalInstruments { get; set; }
    public int ActiveInstruments { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int FilledOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int TotalTrades { get; set; }
    public decimal TotalWalletBalance { get; set; }
    public decimal TotalAvailableBalance { get; set; }
    public decimal TotalReservedBalance { get; set; }
}
