namespace VoxTrade.Dashboard.DTOs;

public class MarketInstrumentSnapshotDto
{
    public int InstrumentId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? BidPrice { get; set; }
    public decimal? AskPrice { get; set; }
    public decimal? LastTradePrice { get; set; }
    public DateTime? QuoteTimestamp { get; set; }
}
