namespace VoxTrade.Admin.DTOs;

public class UpdateUserAdminRequestDto
{
    public string? FirstNameEn { get; set; }
    public string? LastNameEn { get; set; }
    public string? FirstNameAr { get; set; }
    public string? LastNameAr { get; set; }
    public string? Username { get; set; }
    public int? RoleId { get; set; }
    public int? PrimaryCurrencyId { get; set; }
    public bool? Is2FaEnabled { get; set; }
}
