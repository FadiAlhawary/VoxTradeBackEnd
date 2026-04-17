using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoxTrade.Models
{
    [Table("voice_commands")]
    public class VoiceCommand
    {
        [Key]
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string? RawText { get; set; }
        public int? LanguageId { get; set; }
        public int? Language2Id { get; set; }
        public bool IsMultiLanguage { get; set; }
        [Column(TypeName = "numeric")]
        public decimal? ConfidenceScore { get; set; }
        public DateTime? RecognizedAt { get; set; }
    }
}