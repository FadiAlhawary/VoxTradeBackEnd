using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("instruments")]
public class Instrument
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("symbol")]
    public string Symbol { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("tick_size")]
    public decimal? TickSize { get; set; }

    [Column("min_quantity")]
    public decimal? MinQuantity { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("instrument_type")]
    public int? InstrumentTypeId { get; set; } // FK to Lookup

    [Column("status")]
    public int? StatusId { get; set; } // FK to Lookup

    [Column("deleted_by")]
    public int? DeletedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [ForeignKey("InstrumentTypeId")]
    public virtual Lookup InstrumentType { get; set; }

    [ForeignKey("StatusId")]
    public virtual Lookup Status { get; set; }
}