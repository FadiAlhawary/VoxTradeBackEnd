namespace VoxTrade.Models.Auth;

public class GoogleRegisterRequest
{
    public string IdToken { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? FirstNameEn { get; set; }
    public string? LastNameEn { get; set; }
    public DateTime? Dob { get; set; }
    public string? PhoneNumber { get; set; }
}
