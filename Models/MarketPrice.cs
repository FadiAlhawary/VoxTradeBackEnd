using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("market_prices")]
public class MarketPrice
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("instrument_id")]
    public int? InstrumentId { get; set; }

    [Column("price")]
    public decimal? Price { get; set; }

    [Column("ask_price")]
    public decimal? AskPrice { get; set; }

    [Column("bid_price")]
    public decimal? BidPrice { get; set; }

    [Column("volume")]
    public decimal? Volume { get; set; }

    [Column("price_time")]
    public DateTime? PriceTime { get; set; }

    [ForeignKey("InstrumentId")]
    public virtual Instrument Instrument { get; set; }
}