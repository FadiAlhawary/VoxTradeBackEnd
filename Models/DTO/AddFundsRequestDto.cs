namespace VoxTrade.Models.DTO
{
    public class AddFundsRequestDto
    {
        public int AdminUserId { get; set; }
        public int TargetUserId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }
}
