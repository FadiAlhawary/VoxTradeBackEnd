using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("orders")]
public class Order
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("instrument_id")]
    public int? InstrumentId { get; set; }

    [Column("payment_method_id")]
    public int? PaymentMethodId { get; set; }

    [Column("order_type_id")]
    public int OrderTypeId { get; set; }

    [Column("action_type_id")]
    public int ActionTypeId { get; set; }

    [Column("quantity")]
    public decimal? Quantity { get; set; }

    [Column("price")]
    public decimal? Price { get; set; }

    [Column("status_id")]
    public int StatusId { get; set; }

    [Column("source_id")]
    public int SourceId { get; set; }

    [Column("voice_command_id")]
    public int? VoiceCommandId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("currency_id")]
    public int? CurrencyId { get; set; }

    [Column("execution_price")]
    public decimal? ExecutionPrice { get; set; }

    // Navigation Properties
    [ForeignKey("UserId")]
    public virtual User User { get; set; }

    [ForeignKey("InstrumentId")]
    public virtual Instrument Instrument { get; set; }

    [ForeignKey("PaymentMethodId")]
    public virtual UserPaymentMethod UserPaymentMethod { get; set; }

    [ForeignKey("VoiceCommandId")]
    public virtual VoiceCommand VoiceCommand { get; set; }

    [ForeignKey("CurrencyId")]
    public virtual Currency Currency { get; set; }

    // Lookups
    [ForeignKey("OrderTypeId")]
    public virtual Lookup OrderType { get; set; }

    [ForeignKey("ActionTypeId")]
    public virtual Lookup ActionType { get; set; }

    [ForeignKey("StatusId")]
    public virtual Lookup Status { get; set; }

    [ForeignKey("SourceId")]
    public virtual Lookup Source { get; set; }
}