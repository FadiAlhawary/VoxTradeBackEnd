using VoxTrade.Models;
using VoxTrade.Models.Auth;

namespace VoxTrade.Services.Interface;

public interface IAuthService
{
    string GenerateJwtToken(User user);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
