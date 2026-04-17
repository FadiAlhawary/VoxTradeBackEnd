using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoxTrade.Models
{
    [Table("market_quotes")]
    public class MarketQuote
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InstrumentId { get; set; }

        [Column(TypeName = "numeric")]
        public decimal? BidPrice { get; set; }

        [Column(TypeName = "numeric")]
        public decimal? AskPrice { get; set; }

        [Column(TypeName = "numeric")]
        public decimal? LastTradePrice { get; set; }

        public DateTime QuoteTimestamp { get; set; }

        public string Source { get; set; } = "finnhub";
    }
}