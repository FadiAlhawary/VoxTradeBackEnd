namespace VoxTrade.Models.DTO
{
    public class PlaceOrderRequestDto
    {
        public int UserId { get; set; }
        public int InstrumentId { get; set; }
        public string? Symbol { get; set; }
        public string Side { get; set; } = "";       // buy / sell
        public string OrderType { get; set; } = "";  // market / limit
        public decimal Quantity { get; set; }
        public decimal? LimitPrice { get; set; }
        public int? CurrencyId { get; set; }
        public string SourceCode { get; set; } = "manual";
    }
}
