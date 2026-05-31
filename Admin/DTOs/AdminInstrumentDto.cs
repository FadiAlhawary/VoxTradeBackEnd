namespace VoxTrade.Admin.DTOs;

public class AdminInstrumentDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? TickSize { get; set; }
    public decimal? MinQuantity { get; set; }
    public int? InstrumentType { get; set; }
    public int? Status { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}
