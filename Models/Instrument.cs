using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoxTrade.Models
{
    [Table("instruments")]
    public class Instrument
    {
        [Key]
        public int Id { get; set; }
        public string? Symbol { get; set; }
        public string? Name { get; set; }
        [Column(TypeName = "numeric")]
        public decimal? TickSize { get; set; }
        [Column(TypeName = "numeric")]
        public decimal? MinQuantity { get; set; }
        public int? InstrumentType { get; set; }
        public int? Status { get; set; }
        public bool IsDeleted { get; set; }
    }
}