using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoxTrade.Models
{
    [Table("currencies")]
    public class Currency
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name_en")]
        public string NameEn { get; set; } = string.Empty;

        [Column("name_ar")]
        public string NameAr { get; set; } = string.Empty;

        [Column("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [Column("usd_rate")]
        public decimal UsdRate { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("is_deleted")]
        public bool? IsDeleted { get; set; }

        [Column("country_id")]
        public int? CountryId { get; set; }
    }
}