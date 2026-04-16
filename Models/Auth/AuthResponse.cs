namespace VoxTrade.Models.Auth;

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Token { get; set; }
    public UserDto User { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string FirstNameEn { get; set; }
    public string LastNameEn { get; set; }
    public string Email { get; set; }
    public DateTime? Dob { get; set; }
}
//test
