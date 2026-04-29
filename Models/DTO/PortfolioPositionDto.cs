namespace VoxTrade.Models.DTO
{
    public class PortfolioPositionDto
    {
        public int PositionId { get; set; }
        public int UserId { get; set; }
        public int InstrumentId { get; set; }
        public string Symbol { get; set; } = "";
        public string InstrumentName { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal AverageCost { get; set; }
        public decimal RealizedPnl { get; set; }
        public decimal? BidPrice { get; set; }
        public decimal? AskPrice { get; set; }
        public decimal? LastTradePrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal MarketValue { get; set; }
        public decimal UnrealizedPnl { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string short_name { get; set; }

    }
}
