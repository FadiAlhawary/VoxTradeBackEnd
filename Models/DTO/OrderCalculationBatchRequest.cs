using System.Collections.Generic;

namespace VoxTrade.Models.DTO
{
    public class OrderCalculationBatchRequest
    {
        public string ToCurrency { get; set; } = "USD";
        public List<OrderCalculationItem> Items { get; set; } = new();
    }
}
