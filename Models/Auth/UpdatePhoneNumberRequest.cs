namespace VoxTrade.Models.Auth
{
    public class UpdatePhoneNumberRequest
    {
        public int UserId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
