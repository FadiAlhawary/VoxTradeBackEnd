namespace VoxTrade.Admin.DTOs;

public class UpdateInstrumentRequestDto
{
    public string? Symbol { get; set; }
    public string? ShortName { get; set; }
    public string? Name { get; set; }
    public decimal? TickSize { get; set; }
    public decimal? MinQuantity { get; set; }
    public int? InstrumentType { get; set; }
    public int? Status { get; set; }
}
