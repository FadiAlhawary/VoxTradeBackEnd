using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("trades")]
public class Trade
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("order_id")]
    public int? OrderId { get; set; }

    [Column("price")]
    public decimal? Price { get; set; }

    [Column("quantity")]
    public decimal? Quantity { get; set; }

    [Column("executed_at")]
    public DateTime? ExecutedAt { get; set; }

    [Column("currency_id")]
    public int? CurrencyId { get; set; }

    [ForeignKey("OrderId")]
    public virtual Order Order { get; set; }

    [ForeignKey("CurrencyId")]
    public virtual Currency Currency { get; set; }
}