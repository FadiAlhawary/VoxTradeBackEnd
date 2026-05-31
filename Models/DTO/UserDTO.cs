namespace VoxTrade.Models.DTO
{
    public class UserDTO
    {
        public int Id { get; set; }

        public string FirstNameEn { get; set; } = string.Empty;
        public string LastNameEn { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;
        public DateTime Dob { get; set; }

        public string PrimaryEmail { get; set; } = string.Empty;
        public string? AltEmail { get; set; }

        public string PrimaryPhoneNumber { get; set; } = string.Empty;
        public string? AltPhoneNumber { get; set; }

        public bool IsPrimaryEmailActive { get; set; }
        public bool IsAltEmailActive { get; set; }

        public bool IsPrimaryPhoneNumberActive { get; set; }
        public bool IsAltPhoneNumberActive { get; set; }

        public int? RoleId { get; set; }
        public string? RoleNameEn { get; set; }

        public bool IsLocked { get; set; }
        public bool IsDeleted { get; set; }
    }
}
