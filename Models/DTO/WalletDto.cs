using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoxTrade.Models.DTO
{
    public class WalletDto
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int UserId { get; set; }
        public int? CurrencyId { get; set; }
        [Column(TypeName = "numeric")]
        public decimal Balance { get; set; } = 0;
        [Column(TypeName = "numeric")]
        public decimal AvailableBalance { get; set; } = 0;
        [Column(TypeName = "numeric")]
        public decimal ReservedBalance { get; set; } = 0;
        public bool Status { get; set; } = true;
        public DateTime UpdatedAt { get; set; }
        public List<WalletHistoryDto> walletHistory { get; set; }
    }
}
