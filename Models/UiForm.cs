using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ui_forms")]
public class UiForm
{
    [Key]
    [Column("form_uiid")]
    public int FormUiId { get; set; }

    [Column("title")]
    public string Title { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("creation_date")]
    public DateTime? CreationDate { get; set; }
}