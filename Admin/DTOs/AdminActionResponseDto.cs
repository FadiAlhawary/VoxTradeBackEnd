namespace VoxTrade.Admin.DTOs;

public class AdminActionResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? Id { get; set; }
}
