using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoxTrade.Models
{
    [Table("orders")]
    public class Order
    {
        [Key]
        public int Id { get; set; }
        public int? UserId { get; set; }
        public int? InstrumentId { get; set; }
        [Required]
        public int OrderTypeId { get; set; }
        [Required]
        public int ActionTypeId { get; set; }
        [Column(TypeName = "numeric")]
        public decimal? Quantity { get; set; }
        [Column(TypeName = "numeric")]
        public decimal? Price { get; set; }
        [Required]
        public int StatusId { get; set; }
        [Required]
        public int SourceId { get; set; }
        public int? VoiceCommandId { get; set; }
        public DateTime? CreatedAt { get; set; }
        [Column(TypeName = "numeric")]
        public decimal? ExecutionPrice { get; set; }
        [Column(TypeName = "numeric")]
        public decimal FilledQuantity { get; set; } = 0;
        [Column(TypeName = "numeric")]
        public decimal? RemainingQuantity { get; set; }
        public string? Side { get; set; }
    }
}