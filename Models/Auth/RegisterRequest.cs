namespace VoxTrade.Models.Auth;

public class RegisterRequest
{
    public string FirstNameEn { get; set; }
    public string LastNameEn { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public DateTime? Dob { get; set; }
    public string PhoneNumber { get; set; }
}
