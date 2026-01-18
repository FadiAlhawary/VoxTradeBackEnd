using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("command_interpretations")]
public class CommandInterpretation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("voice_command_id")]
    public int? VoiceCommandId { get; set; }

    [Column("intent")]
    public string Intent { get; set; }

    [Column("instrument_id")]
    public int? InstrumentId { get; set; }

    [Column("quantity")]
    public decimal? Quantity { get; set; }

    [Column("price")]
    public decimal? Price { get; set; }

    [Column("currency_id")]
    public int? CurrencyId { get; set; }

    [Column("parsed_successfully")]
    public bool? ParsedSuccessfully { get; set; }

    [Column("error_message")]
    public string ErrorMessage { get; set; }

    [ForeignKey("VoiceCommandId")]
    public virtual VoiceCommand VoiceCommand { get; set; }

    [ForeignKey("InstrumentId")]
    public virtual Instrument Instrument { get; set; }

    [ForeignKey("CurrencyId")]
    public virtual Currency Currency { get; set; }
}