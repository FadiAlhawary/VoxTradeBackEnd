namespace VoxTrade.Models.DTO;

public class TransferMoneyRequestDto
{
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
