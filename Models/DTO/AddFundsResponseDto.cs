namespace VoxTrade.Models.DTO
{
    public class AddFundsResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";

        public int? TargetUserId { get; set; }
        public decimal? Amount { get; set; }

        public decimal? BalanceBefore { get; set; }
        public decimal? BalanceAfter { get; set; }

        public decimal? AvailableBefore { get; set; }
        public decimal? AvailableAfter { get; set; }

        public decimal? ReservedBefore { get; set; }
        public decimal? ReservedAfter { get; set; }
    }
}
