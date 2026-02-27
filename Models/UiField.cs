using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ui_fields")]
public class UiField
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("field_name")]
    public string FieldName { get; set; }

    [Column("field_type")]
    public string FieldType { get; set; }

    [Column("is_readonly")]
    public bool? IsReadOnly { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

    [Column("is_sensitive")]
    public bool? IsSensitive { get; set; }

    [Column("creation_date")]
    public DateTime? CreationDate { get; set; }

    [Column("label")]
    public string Label { get; set; }

    [Column("system_name")]
    public string SystemName { get; set; }

    [Column("object_name")]
    public string ObjectName { get; set; }
}