namespace VoxTrade.PaymentMethods.DTOs;

/// <summary>Available payment method from catalog (payment_method table).</summary>
public class PaymentMethodTypeDto
{
    public int Id { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string MethodType { get; set; } = string.Empty;
}
