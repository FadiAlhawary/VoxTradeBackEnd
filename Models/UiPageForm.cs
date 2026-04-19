using System.ComponentModel.DataAnnotations.Schema;

[Table("ui_page_forms")]
public class UiPageForm
{
    
    [Column("page_id")]
    public int PageId { get; set; }

    [Column("form_id")]
    public int FormId { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("creation_date")]
    public DateTime? CreationDate { get; set; }

    [ForeignKey("PageId")]
    public virtual MenuLink Page { get; set; }

    [ForeignKey("FormId")]
    public virtual UiForm Form { get; set; }
}