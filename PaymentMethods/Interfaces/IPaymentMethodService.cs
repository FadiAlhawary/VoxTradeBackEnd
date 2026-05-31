using VoxTrade.PaymentMethods.DTOs;

namespace VoxTrade.PaymentMethods.Interfaces;

public interface IPaymentMethodService
{
    Task<List<PaymentMethodTypeDto>> GetAvailableTypesAsync();
    Task<List<UserPaymentMethodDto>> GetUserPaymentMethodsAsync(int userId);
    Task<UserPaymentMethodDto?> GetUserPaymentMethodByIdAsync(int userId, int userPaymentMethodId);
    Task<PaymentMethodActionResponseDto> AddUserPaymentMethodAsync(int userId, AddUserPaymentMethodRequestDto request);
    Task<PaymentMethodActionResponseDto> UpdateUserPaymentMethodAsync(
        int userId,
        int userPaymentMethodId,
        UpdateUserPaymentMethodRequestDto request);
    Task<PaymentMethodActionResponseDto> RemoveUserPaymentMethodAsync(int userId, int userPaymentMethodId);
}
