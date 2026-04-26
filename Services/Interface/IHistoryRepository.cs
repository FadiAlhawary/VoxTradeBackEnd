using VoxTrade.Models.DTO;

namespace VoxTrade.Services.Interface
{
    public interface IHistoryRepository
    {
        Task<List<OrderHistoryDto>> GetOrderHistory(int userId, bool activeOnly = false);
        Task<List<TradeHistoryDto>> GetTradeHistory(int userId);
        Task<List<WalletHistoryDto>> GetWalletHistory(int userId);

        Task<bool> CancelPendingOrder(int userId, int orderId);

        Task<List<LookupItemDto>> GetOrderStatuses();
        Task<List<LookupItemDto>> GetOrderTypes();
        Task<List<LookupItemDto>> GetOrderActions();
        Task<List<WalletHistoryDto>> GetWalletHistoryWithDate(
    int userId,
    DateTime? from = null,
    DateTime? to = null);
    }
}
