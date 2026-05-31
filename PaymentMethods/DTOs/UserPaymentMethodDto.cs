namespace VoxTrade.PaymentMethods.DTOs;

public class UserPaymentMethodDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PaymentMethodId { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string MethodType { get; set; } = string.Empty;
    public string? AttributeValue1 { get; set; }
    public string? AttributeValue2 { get; set; }
    public DateTime? CreatedAt { get; set; }
}
