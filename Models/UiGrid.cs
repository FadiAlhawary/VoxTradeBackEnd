using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ui_grids")]
public class UiGrid
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("title")]
    public string Title { get; set; }

    [Column("data_source")]
    public string DataSource { get; set; }

    [Column("condition")]
    public string Condition { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("creation_date")]
    public DateTime? CreationDate { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }
}