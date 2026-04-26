namespace VoxTrade.Models.DTO
{
    public class PortfolioProfitLossPointDto
    {
        public DateTime Time { get; set; }
        public decimal TotalValue { get; set; }
        public decimal CashBalance { get; set; }
        public decimal PositionsValue { get; set; }
        public decimal RealizedPnl { get; set; }
        public decimal UnrealizedPnl { get; set; }
        public decimal ProfitLoss { get; set; }
    }
}
