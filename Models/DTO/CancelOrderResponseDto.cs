namespace VoxTrade.Models.DTO
{
    public class CancelOrderResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int? OrderId { get; set; }
    }
}
