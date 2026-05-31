namespace VoxTrade.Admin.DTOs;

public class AdminAuditLogDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? Entity { get; set; }
    public int? EntityId { get; set; }
    public string? ActionCode { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
