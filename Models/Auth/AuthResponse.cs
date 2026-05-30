using VoxTrade.Models.DTO;

namespace VoxTrade.Models.Auth;

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Token { get; set; }
    public UserDTO User { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public int? PendingUserId { get; set; }
}
