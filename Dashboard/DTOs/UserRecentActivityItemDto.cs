namespace VoxTrade.Dashboard.DTOs;

public class UserRecentActivityItemDto
{
    public string ActivityType { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public decimal? Amount { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
