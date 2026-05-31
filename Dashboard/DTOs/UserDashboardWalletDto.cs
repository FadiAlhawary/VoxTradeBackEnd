namespace VoxTrade.Dashboard.DTOs;

public class UserDashboardWalletDto
{
    public int WalletId { get; set; }
    public int UserId { get; set; }
    public int? CurrencyId { get; set; }
    public string? CurrencySymbol { get; set; }
    public decimal Balance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal ReservedBalance { get; set; }
    public bool Status { get; set; }
    public bool IsFrozen => !Status;
    public string? FreezeReason { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
