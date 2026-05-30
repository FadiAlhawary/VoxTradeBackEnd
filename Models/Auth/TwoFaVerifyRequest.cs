namespace VoxTrade.Models.Auth
{
    public class TwoFaVerifyRequest
    {
        public int UserId { get; set; }
        public string Code { get; set; } = string.Empty;
    }
}
