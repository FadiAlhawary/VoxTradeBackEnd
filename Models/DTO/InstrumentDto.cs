namespace VoxTrade.Models.DTO
{
    public class InstrumentDto
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal? TickSize { get; set; }
        public decimal? MinQuantity { get; set; }

        public int? InstrumentTypeId { get; set; }
        public string? InstrumentType { get; set; }

        public int? StatusId { get; set; }
        public string? Status { get; set; }
    }
}
