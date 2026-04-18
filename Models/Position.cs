using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoxTrade.Models
{
    [Table("positions")]
    public class Position
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int InstrumentId { get; set; }

        [Column(TypeName = "numeric")]
        public decimal Quantity { get; set; } = 0;

        [Column(TypeName = "numeric")]
        public decimal AverageCost { get; set; } = 0;

        [Column(TypeName = "numeric")]
        public decimal RealizedPnl { get; set; } = 0;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}