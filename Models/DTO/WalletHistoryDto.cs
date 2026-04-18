namespace VoxTrade.Models.DTO
{
    public class WalletHistoryDto
    {
        public int Id { get; set; }
        public int WalletId { get; set; }
        public int UserId { get; set; }
        public int? OrderId { get; set; }
        public int? TradeId { get; set; }

        public string TransactionType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
