using System.ComponentModel.DataAnnotations.Schema;

[Table("ui_group_grids")]
public class UiGroupGrid
{
    
    [Column("group_id")]
    public int GroupId { get; set; }

    [Column("grid_id")]
    public int GridId { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

    [Column("creation_date")]
    public DateTime? CreationDate { get; set; }

    [ForeignKey("GroupId")]
    public virtual UiGroup Group { get; set; }

    [ForeignKey("GridId")]
    public virtual UiGrid Grid { get; set; }
}