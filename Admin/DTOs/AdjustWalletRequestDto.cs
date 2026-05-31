namespace VoxTrade.Admin.DTOs;

public class AdjustWalletRequestDto
{
    public int AdminUserId { get; set; }
    public int TargetUserId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
