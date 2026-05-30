namespace VoxTrade.Models.DTO
{
    public class HoldingSummaryDto
    {
        public string Symbol { get; set; } = "";
        public int? InstrumentId { get; set; }
        public decimal QuantityOwned { get; set; }
        public decimal ValueUsd { get; set; }
    }
}
