namespace VoxTrade.Models.DTO
{
    public class CurrencyConversionRequest
    {
        public decimal Amount { get; set; }
        public string FromCurrency { get; set; } = "USD";
        public string ToCurrency { get; set; } = "USD";
    }
}
