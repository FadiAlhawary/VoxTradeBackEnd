namespace VoxTrade.Admin.DTOs;

public class AdminUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstNameEn { get; set; } = string.Empty;
    public string LastNameEn { get; set; } = string.Empty;
    public string? FirstNameAr { get; set; }
    public string? LastNameAr { get; set; }
    public int? RoleId { get; set; }
    public string? RoleName { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsDeactivated { get; set; }
    public bool IsLocked { get; set; }
    public string? LockReason { get; set; }
    public bool IsLoggedIn { get; set; }
    public bool Is2FaEnabled { get; set; }
    public int? PrimaryCurrencyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public AdminWalletDto? Wallet { get; set; }
}
