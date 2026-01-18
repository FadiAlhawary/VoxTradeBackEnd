using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("contact_info")]
public class ContactInfo
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("primary_email")]
    public string PrimaryEmail { get; set; }

    [Column("alt_email")]
    public string AltEmail { get; set; }

    [Column("primary_phone_number")]
    public string PrimaryPhoneNumber { get; set; }

    [Column("alt_phone_number")]
    public string AltPhoneNumber { get; set; }

    [Column("is_primary_email_active")]
    public bool? IsPrimaryEmailActive { get; set; }

    [Column("is_alt_email_active")]
    public bool? IsAltEmailActive { get; set; }

    [Column("is_primary_phone_number_active")]
    public bool? IsPrimaryPhoneActive { get; set; }

    [Column("is_alt_phone_number_active")]
    public bool? IsAltPhoneActive { get; set; }

    [Column("fax")]
    public string Fax { get; set; }

    [Column("is_fax_active")]
    public bool? IsFaxActive { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; }
}