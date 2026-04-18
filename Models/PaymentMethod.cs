using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("payment_method")]
public class PaymentMethod
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("mehtod_name")] // Note: Typo in database 'mehtod' vs 'method'
    public string MethodName { get; set; }

    [Column("method_type")]
    public string MethodType { get; set; }

    [Column("form_uiid")]
    public int? FormUiId { get; set; }

    [Column("status")]
    public int? Status { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public int? DeletedBy { get; set; }

    [Column("status_group")]
    public int? StatusGroup { get; set; }
}