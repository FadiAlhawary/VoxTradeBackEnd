using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("first_name_en")]
    public string FirstNameEn { get; set; }

    [Column("last_name_en")]
    public string LastNameEn { get; set; }

    [Column("first_name_ar")]
    public string FirstNameAr { get; set; }

    [Column("last_name_ar")]
    public string LastNameAr { get; set; }

    [Column("username")]
    public string Username { get; set; }

    [NotMapped]
    public string Email { get; set; }

    [NotMapped]
    public string PhoneNumber { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

    [NotMapped]
    public string Token { get; set; }

    [Column("last_login_date")]
    public DateTime? LastLoginDate { get; set; }

    [Column("is_logged_in")]
    public bool IsLoggedIn { get; set; }

    [Column("password")]
    public string Password { get; set; }

    [Column("dob")]
    public DateTime? Dob { get; set; } // Using DateTime for Date type

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("delete_at")]
    public DateTime? DeleteAt { get; set; }

    [Column("deleted_by")]
    public int? DeletedBy { get; set; }

    [Column("primary_currency_id")]
    public int? PrimaryCurrencyId { get; set; }

    // Navigation Properties
    [ForeignKey("PrimaryCurrencyId")]
    public virtual Currency PrimaryCurrency { get; set; }

    public virtual ContactInfo ContactInfo { get; set; }
}