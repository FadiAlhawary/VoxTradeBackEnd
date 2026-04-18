using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("lookup")]
public class Lookup
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("lookup_group_id")]
    public int LookupGroupId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("code")]
    public string Code { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    //[Column("is_active")]
    //public bool? IsActive { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public int? DeletedBy { get; set; }
}