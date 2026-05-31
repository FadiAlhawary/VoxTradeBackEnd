namespace VoxTrade.Admin.DTOs;

public class AdminTradeDto
{
    public int Id { get; set; }
    public int? OrderId { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public int? InstrumentId { get; set; }
    public string? Symbol { get; set; }
    public string? Side { get; set; }
    public decimal? Price { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? TradeValue { get; set; }
    public DateTime? ExecutedAt { get; set; }
}
