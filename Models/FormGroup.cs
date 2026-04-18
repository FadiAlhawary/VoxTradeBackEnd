using System.ComponentModel.DataAnnotations.Schema;

[Table("form_groups")]
public class FormGroup
{
    
    [Column("form_id")]
    public int FormId { get; set; }

    [Column("group_id")]
    public int GroupId { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("creation_date")]
    public DateTime? CreationDate { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

}