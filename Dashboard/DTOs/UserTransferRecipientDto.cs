namespace VoxTrade.Dashboard.DTOs;

/// <summary>Lightweight user row for send-money / transfer picker.</summary>
public class UserTransferRecipientDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstNameEn { get; set; } = string.Empty;
    public string LastNameEn { get; set; } = string.Empty;
    public int? WalletId { get; set; }
}
