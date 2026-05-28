using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VoxTrade.Models;
using VoxTrade.Services.Interface;

namespace VoxTrade.Services.Implementation;

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;

    public AuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateJwtToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "your-256-bit-secret-key-that-is-very-long-and-secure";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "VoxTrade";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "VoxTrade";
        var jwtExpireMinutes = int.Parse(_configuration["Jwt:ExpireMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("FirstName", user.FirstNameEn ?? ""),
            new Claim("LastName", user.LastNameEn ?? ""),
            new Claim("Email", user.Username) // Using username as email for now
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwtExpireMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GeneratePasswordResetToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "your-256-bit-secret-key-that-is-very-long-and-secure";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "VoxTrade";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "VoxTrade";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("purpose", "password-reset")
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public int? ValidatePasswordResetToken(string token)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "your-256-bit-secret-key-that-is-very-long-and-secure";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "VoxTrade";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "VoxTrade";

        var tokenHandler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, parameters, out _);
            var purpose = principal.FindFirst("purpose")?.Value;
            var userIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.Equals(purpose, "password-reset", StringComparison.Ordinal))
            {
                return null;
            }

            return int.TryParse(userIdValue, out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
