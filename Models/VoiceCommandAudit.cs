using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("voice_command_audit")]
public class VoiceCommandAudit
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("voice_command_id")]
    public int? VoiceCommandId { get; set; }

    [Column("step")]
    public string Step { get; set; }

    [Column("details")]
    public string Details { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("VoiceCommandId")]
    public virtual VoiceCommand VoiceCommand { get; set; }
}