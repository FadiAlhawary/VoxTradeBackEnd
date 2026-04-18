using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoxTrade.Models
{
    [Table("trades")]
    public class Trade
    {
        [Key]
        public int Id { get; set; }
        public int? OrderId { get; set; }
        [Column(TypeName = "numeric")]
        public decimal? Price { get; set; }
        [Column(TypeName = "numeric")]
        public decimal? Quantity { get; set; }
        public DateTime? ExecutedAt { get; set; }
        public int? CurrencyId { get; set; }
        public int? InstrumentId { get; set; }
        public int? UserId { get; set; }
        public int? WalletId { get; set; }
        public string? Side { get; set; }
        [Column(TypeName = "numeric")]
        public decimal? TradeValue { get; set; }
    }
}