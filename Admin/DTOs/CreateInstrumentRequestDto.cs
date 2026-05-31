namespace VoxTrade.Admin.DTOs;

public class CreateInstrumentRequestDto
{
    public string Symbol { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? TickSize { get; set; }
    public decimal? MinQuantity { get; set; }
    public int? InstrumentType { get; set; }
    public int? Status { get; set; }
}
