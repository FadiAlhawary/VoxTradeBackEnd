namespace VoxTrade.Admin.DTOs;

public class AdminWalletDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Username { get; set; }
    public int? CurrencyId { get; set; }
    public string? CurrencySymbol { get; set; }
    public decimal Balance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal ReservedBalance { get; set; }
    /// <summary>True when wallet is active; false when frozen.</summary>
    public bool Status { get; set; }

    /// <summary>True when <see cref="Status"/> is false (frozen).</summary>
    public bool IsFrozen => !Status;

    public string? FreezeReason { get; set; }
    public DateTime UpdatedAt { get; set; }
}
