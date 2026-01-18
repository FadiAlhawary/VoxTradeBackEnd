using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("languages")]
public class Language
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name_en")]
    public string NameEn { get; set; }

    [Column("name_ar")]
    public string NameAr { get; set; }
}