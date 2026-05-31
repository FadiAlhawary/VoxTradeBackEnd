namespace VoxTrade.Admin.DTOs;

public class CreateCurrencyRequestDto
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal UsdRate { get; set; }
}
