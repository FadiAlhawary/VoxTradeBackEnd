using VoxTrade.Models.DTO;

namespace VoxTrade.Services.Interface
{
    public interface IPortfolioRepository
    {
        Task<List<PortfolioPositionDto>> GetPortfolio(int userId);
        Task<HoldingSummaryDto> GetHoldingSummary(int userId, string symbol);
        Task<List<PortfolioProfitLossPointDto>> GetProfitLossChart(
    int userId,
    DateTime? from = null,
    DateTime? to = null);
    }
}
