namespace VoxTrade.PaymentMethods.DTOs;

public class PaymentMethodActionResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? Id { get; set; }
    public UserPaymentMethodDto? PaymentMethod { get; set; }
}
