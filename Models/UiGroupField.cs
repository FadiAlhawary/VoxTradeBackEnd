using System.ComponentModel.DataAnnotations.Schema;

[Table("ui_group_fields")]
public class UiGroupField
{
    // Composite Key: Need to configure in OnModelCreating
    [Column("group_id")]
    public int GroupId { get; set; }

    [Column("field_id")]
    public int FieldId { get; set; }

    [Column("visibility_rule")]
    public string VisibilityRule { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

    [Column("creation_date")]
    public DateTime? CreationDate { get; set; }

    [ForeignKey("GroupId")]
    public virtual UiGroup Group { get; set; }

    [ForeignKey("FieldId")]
    public virtual UiField Field { get; set; }
}