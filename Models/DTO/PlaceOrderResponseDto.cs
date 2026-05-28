namespace VoxTrade.Models.DTO
{
    public class PlaceOrderResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int? OrderId { get; set; }
        public string? Status { get; set; }
        public decimal? Price { get; set; }
    }
}
