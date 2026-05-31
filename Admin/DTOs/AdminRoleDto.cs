namespace VoxTrade.Admin.DTOs;

public class AdminRoleDto
{
    public int Id { get; set; }
    public string RoleNameEn { get; set; } = string.Empty;
    public string RoleNameAr { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }
    public bool AllowDelete { get; set; }
    public bool AllowCreate { get; set; }
    public bool AllowEdit { get; set; }
    public bool AllowSuperView { get; set; }
    public bool LockAllUser { get; set; }
    public bool IsDeleted { get; set; }
}
