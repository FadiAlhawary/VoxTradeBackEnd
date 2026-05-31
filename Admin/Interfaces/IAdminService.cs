using VoxTrade.Admin.DTOs;

namespace VoxTrade.Admin.Interfaces;

public interface IAdminService
{
    Task<AdminDashboardDto> GetDashboard();

    Task<List<AdminUserDto>> GetUsers();
    Task<AdminUserDto?> GetUserById(int userId);
    Task<AdminActionResponseDto> UpdateUser(int adminUserId, int targetUserId, UpdateUserAdminRequestDto request);
    Task<AdminActionResponseDto> ChangeUserRole(int adminUserId, int targetUserId, int roleId);
    Task<AdminActionResponseDto> DeactivateUser(int adminUserId, int targetUserId);
    Task<AdminActionResponseDto> RestoreUser(int adminUserId, int targetUserId);
    Task<AdminActionResponseDto> LockUser(int adminUserId, int targetUserId, string? reason);
    Task<AdminActionResponseDto> UnlockUser(int adminUserId, int targetUserId);

    Task<List<AdminWalletDto>> GetWallets();
    Task<AdminWalletDto?> GetUserWallet(int userId);
    Task<AdminActionResponseDto> AddFunds(AdjustWalletRequestDto request);
    Task<AdminActionResponseDto> DeductFunds(AdjustWalletRequestDto request);
    Task<AdminActionResponseDto> FreezeWallet(int adminUserId, int targetUserId, string? reason);
    Task<AdminActionResponseDto> UnfreezeWallet(int adminUserId, int targetUserId);

    Task<List<AdminInstrumentDto>> GetInstruments();
    Task<AdminActionResponseDto> AddInstrument(CreateInstrumentRequestDto request);
    Task<AdminActionResponseDto> UpdateInstrument(int instrumentId, UpdateInstrumentRequestDto request);
    Task<AdminActionResponseDto> DeactivateInstrument(int adminUserId, int instrumentId);
    Task<AdminActionResponseDto> RestoreInstrument(int instrumentId);

    Task<List<AdminOrderDto>> GetOrders();
    Task<List<AdminOrderDto>> GetUserOrders(int userId);
    Task<AdminActionResponseDto> CancelUserOrder(int adminUserId, int orderId);

    Task<List<AdminTradeDto>> GetTrades();
    Task<List<AdminTradeDto>> GetUserTrades(int userId);

    Task<List<AdminRoleDto>> GetRoles();
    Task<AdminActionResponseDto> AddRole(CreateRoleRequestDto request);
    Task<AdminActionResponseDto> UpdateRole(int roleId, CreateRoleRequestDto request);
    Task<AdminActionResponseDto> DeactivateRole(int roleId);
    Task<AdminActionResponseDto> RestoreRole(int roleId);

    Task<List<AdminCurrencyDto>> GetCurrencies();
    Task<AdminActionResponseDto> AddCurrency(CreateCurrencyRequestDto request);
    Task<AdminActionResponseDto> UpdateCurrency(int currencyId, CreateCurrencyRequestDto request);
    Task<AdminActionResponseDto> DeactivateCurrency(int currencyId);
    Task<AdminActionResponseDto> RestoreCurrency(int currencyId);

    Task<List<AdminAuditLogDto>> GetAuditLogs();
}
