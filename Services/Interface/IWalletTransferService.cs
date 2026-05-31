using VoxTrade.Models.DTO;

namespace VoxTrade.Services.Interface;

public interface IWalletTransferService
{
    Task<TransferMoneyResponseDto> TransferMoneyAsync(TransferMoneyRequestDto request);
}
