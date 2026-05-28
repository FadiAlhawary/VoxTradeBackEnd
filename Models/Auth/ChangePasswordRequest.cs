namespace VoxTrade.Models.Auth
{
    public class ChangePasswordRequest
    {
        public int UserId { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }
}
