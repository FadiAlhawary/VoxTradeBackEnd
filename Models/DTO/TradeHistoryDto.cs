namespace VoxTrade.Models.DTO
{
    public class TradeHistoryDto
    {
        public int Id { get; set; }
        public int? OrderId { get; set; }
        public int? UserId { get; set; }
        public int? InstrumentId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string InstrumentName { get; set; } = string.Empty;

        public string Side { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? TradeValue { get; set; }
        public DateTime ExecutedAt { get; set; }
    }
}
