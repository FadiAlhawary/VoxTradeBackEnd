using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoxTrade.Models
{
    [Table("wallet_history")]
    public class WalletHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int WalletId { get; set; }

        [Required]
        public int UserId { get; set; }

        public int? OrderId { get; set; }
        public int? TradeId { get; set; }

        [Required]
        public string TransactionType { get; set; } = string.Empty;

        [Column(TypeName = "numeric")]
        public decimal Amount { get; set; }

        [Column(TypeName = "numeric")]
        public decimal BalanceBefore { get; set; }

        [Column(TypeName = "numeric")]
        public decimal BalanceAfter { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}