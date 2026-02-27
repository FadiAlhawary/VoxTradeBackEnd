using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("voice_commands")]
public class VoiceCommand
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("raw_text")]
    public string RawText { get; set; }

    [Column("language_id")]
    public int? LanguageId { get; set; }

    [Column("language_2_id")]
    public int? Language2Id { get; set; }

    [Column("is_multi_language")]
    public bool? IsMultiLanguage { get; set; }

    [Column("confidence_score")]
    public decimal? ConfidenceScore { get; set; }

    [Column("recognized_at")]
    public DateTime? RecognizedAt { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; }

    [ForeignKey("LanguageId")]
    public virtual Language Language { get; set; }

    [ForeignKey("Language2Id")]
    public virtual Language Language2 { get; set; }
}