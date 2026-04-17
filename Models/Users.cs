using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoxTrade.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string FirstNameEn { get; set; } = string.Empty;
        [Required]
        public string LastNameEn { get; set; } = string.Empty;
        [Required]
        public string FirstNameAr { get; set; } = string.Empty;
        [Required]
        public string LastNameAr { get; set; } = string.Empty;
        [Required]
        public string Username { get; set; } = string.Empty;
        public int? RoleId { get; set; }
        public string? Token { get; set; }
        public DateTime LastLoginDate { get; set; }
        public bool IsLoggedIn { get; set; }
        [Required]
        public string Password { get; set; } = string.Empty;
        public DateTime Dob { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public int? PrimaryCurrencyId { get; set; }
    }
}