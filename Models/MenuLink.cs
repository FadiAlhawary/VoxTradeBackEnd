using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("menu_links")]
public class MenuLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("title")]
    public string Title { get; set; }

    [Column("parent_menu_id")]
    public int? ParentMenuId { get; set; }

    [Column("order_index")]
    public int? OrderIndex { get; set; }

    [Column("url")]
    public string Url { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

    [Column("creation_date")]
    public DateTime? CreationDate { get; set; }

    [ForeignKey("ParentMenuId")]
    public virtual MenuLink ParentMenu { get; set; }
}