namespace VoxTrade.PaymentMethods.DTOs;

public class AddUserPaymentMethodRequestDto
{
    public int PaymentMethodId { get; set; }
    public string? AttributeValue1 { get; set; }
    public string? AttributeValue2 { get; set; }
}
