using VoxTrade.Models.DTO;

namespace VoxTrade.Services.Interface
{
    public interface IWalletRepo
    {
        Task<WalletDto> GetWallet(int userId, bool WithHisory);
    }
}
