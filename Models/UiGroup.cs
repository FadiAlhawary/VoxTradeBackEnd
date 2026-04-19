using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ui_groups")]
public class UiGroup
{
    [Key]
    [Column("group_id")]
    public int GroupId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("creation_date")]
    public DateTime? CreationDate { get; set; }
}