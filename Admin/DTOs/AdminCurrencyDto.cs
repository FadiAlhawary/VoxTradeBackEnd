namespace VoxTrade.Admin.DTOs;

public class AdminCurrencyDto
{
    public int Id { get; set; }
    public string? NameEn { get; set; }
    public string? NameAr { get; set; }
    public string? Symbol { get; set; }
    public decimal? UsdRate { get; set; }
    public bool IsDeleted { get; set; }
}
