using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("countries")]
public class Country
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name_en")]
    public string NameEn { get; set; }

    [Column("name_ar")]
    public string NameAr { get; set; }

    [Column("region")]
    public string Region { get; set; }

    [Column("primary_language_id")]
    public int? PrimaryLanguageId { get; set; }

    [Column("secondary_language_id")]
    public int? SecondaryLanguageId { get; set; }

    [ForeignKey("PrimaryLanguageId")]
    public virtual Language PrimaryLanguage { get; set; }

    [ForeignKey("SecondaryLanguageId")]
    public virtual Language SecondaryLanguage { get; set; }
}