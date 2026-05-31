namespace VoxTrade.Models.DTO
{
    public class UserSearchResultDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstNameEn { get; set; } = string.Empty;
        public string LastNameEn { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
