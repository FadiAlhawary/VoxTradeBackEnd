namespace VoxTrade.Models.DTO
{
    public class MarketTickDto
    {
        public string Symbol { get; set; } = default!;
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
        public long TimestampUnixMs { get; set; }
        public string[] Conditions { get; set; } = Array.Empty<string>();
        public string Source { get; set; } = "finnhub";
    }
}
