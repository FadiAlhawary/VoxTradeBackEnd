namespace VoxTrade.Models.DTO
{
    public class OrderHistoryDto
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public int? InstrumentId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string InstrumentName { get; set; } = string.Empty;

        public int OrderTypeId { get; set; }
        public string OrderType { get; set; } = string.Empty;

        public int ActionTypeId { get; set; }
        public string ActionType { get; set; } = string.Empty;

        public int StatusId { get; set; }
        public string Status { get; set; } = string.Empty;

        public int SourceId { get; set; }
        public string Source { get; set; } = string.Empty;

        public decimal? Quantity { get; set; }
        public decimal FilledQuantity { get; set; }
        public decimal? RemainingQuantity { get; set; }

        public decimal? Price { get; set; }
        public decimal? LimitPrice { get; set; }
        public decimal? StopPrice { get; set; }
        public decimal? ExecutionPrice { get; set; }
        public decimal? AverageFillPrice { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }

        public bool CanCancel { get; set; }
    }

}
