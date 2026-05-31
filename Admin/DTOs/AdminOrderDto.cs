namespace VoxTrade.Admin.DTOs;

public class AdminOrderDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public int? InstrumentId { get; set; }
    public string? Symbol { get; set; }
    public string? Side { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? LimitPrice { get; set; }
    public decimal? ExecutionPrice { get; set; }
    public decimal FilledQuantity { get; set; }
    public decimal? RemainingQuantity { get; set; }
    public string? Status { get; set; }
    public string? OrderType { get; set; }
    public DateTime? CreatedAt { get; set; }
}
