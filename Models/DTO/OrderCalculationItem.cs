namespace VoxTrade.Models.DTO
{
    public class OrderCalculationItem
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string FromCurrency { get; set; } = "USD";
    }
}
