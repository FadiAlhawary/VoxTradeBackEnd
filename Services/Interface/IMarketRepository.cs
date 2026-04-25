using VoxTrade.Models.DTO;

namespace VoxTrade.Services.Interface
{
    public interface IMarketRepository
    {
        Task<PlaceOrderResponseDto> PlaceOrder(PlaceOrderRequestDto request);
    }
}
