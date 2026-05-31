namespace VoxTrade.Models.DTO;

public class TransferMoneyResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? FromUserId { get; set; }
    public int? ToUserId { get; set; }
    public decimal? Amount { get; set; }
    public decimal? SenderBalanceAfter { get; set; }
    public decimal? SenderAvailableBalanceAfter { get; set; }
    public decimal? ReceiverBalanceAfter { get; set; }
    public decimal? ReceiverAvailableBalanceAfter { get; set; }
}
