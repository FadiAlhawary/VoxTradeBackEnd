using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ui_grid_columns")]
public class UiGridColumn
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("grid_id")]
    public int? GridId { get; set; }

    [Column("label")]
    public string Label { get; set; }

    [Column("system_name")]
    public string SystemName { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("creation_date")]
    public DateTime? CreationDate { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

    [Column("order_index")]
    public int? OrderIndex { get; set; }

    [Column("is_sortable")]
    public bool? IsSortable { get; set; }

    [Column("filter_type")]
    public string FilterType { get; set; }

    [ForeignKey("GridId")]
    public virtual UiGrid Grid { get; set; }
}