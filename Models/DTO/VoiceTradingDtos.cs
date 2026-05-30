namespace VoxTrade.Models.DTO
{
    public class VoiceInstrumentValidationRequest
    {
        public string Symbol { get; set; } = string.Empty;
    }

    public class VoiceInstrumentValidationResponse
    {
        public bool Exists { get; set; }
        public int? InstrumentId { get; set; }
        public string? Symbol { get; set; }
        public string? Name { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ExecuteVoiceOrderRequest
    {
        public string Intent { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public string OrderType { get; set; } = "market";
        public decimal? Price { get; set; }
        public decimal? Confidence { get; set; }
        public string? RawText { get; set; }
        public string? CurrencySymbol { get; set; } = "USD";
        public string? Language { get; set; }
    }

    public class ExecuteVoiceOrderResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? OrderId { get; set; }
        public int? VoiceCommandId { get; set; }
        public int? InstrumentId { get; set; }
        public string? Symbol { get; set; }
        public decimal? ExecutedPrice { get; set; }
    }
}
