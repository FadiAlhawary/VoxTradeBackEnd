using Microsoft.AspNetCore.Mvc;
using VoxTrade.Admin.DTOs;
using VoxTrade.Admin.Interfaces;

namespace VoxTrade.Admin.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _adminService.GetDashboard();
        return Ok(result);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var result = await _adminService.GetUsers();
        return Ok(result);
    }

    [HttpGet("users/{userId:int}")]
    public async Task<IActionResult> GetUserById(int userId)
    {
        var result = await _adminService.GetUserById(userId);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    [HttpPut("users/{targetUserId:int}")]
    public async Task<IActionResult> UpdateUser(
        int targetUserId,
        [FromQuery] int adminUserId,
        [FromBody] UpdateUserAdminRequestDto request)
    {
        var result = await _adminService.UpdateUser(adminUserId, targetUserId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{targetUserId:int}/role/{roleId:int}")]
    public async Task<IActionResult> ChangeUserRole(
        int targetUserId,
        int roleId,
        [FromQuery] int adminUserId)
    {
        var result = await _adminService.ChangeUserRole(adminUserId, targetUserId, roleId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{targetUserId:int}/deactivate")]
    public async Task<IActionResult> DeactivateUser(int targetUserId, [FromQuery] int adminUserId)
    {
        var result = await _adminService.DeactivateUser(adminUserId, targetUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{targetUserId:int}/restore")]
    public async Task<IActionResult> RestoreUser(int targetUserId, [FromQuery] int adminUserId)
    {
        var result = await _adminService.RestoreUser(adminUserId, targetUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{targetUserId:int}/lock")]
    public async Task<IActionResult> LockUser(
        int targetUserId,
        [FromQuery] int adminUserId,
        [FromQuery] string? reason)
    {
        var result = await _adminService.LockUser(adminUserId, targetUserId, reason);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{targetUserId:int}/unlock")]
    public async Task<IActionResult> UnlockUser(int targetUserId, [FromQuery] int adminUserId)
    {
        var result = await _adminService.UnlockUser(adminUserId, targetUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("wallets")]
    public async Task<IActionResult> GetWallets()
    {
        var result = await _adminService.GetWallets();
        return Ok(result);
    }

    [HttpGet("users/{userId:int}/wallet")]
    public async Task<IActionResult> GetUserWallet(int userId)
    {
        var result = await _adminService.GetUserWallet(userId);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    [HttpPost("wallets/add-funds")]
    public async Task<IActionResult> AddFunds([FromBody] AdjustWalletRequestDto request)
    {
        var result = await _adminService.AddFunds(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("wallets/deduct-funds")]
    public async Task<IActionResult> DeductFunds([FromBody] AdjustWalletRequestDto request)
    {
        var result = await _adminService.DeductFunds(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{targetUserId:int}/wallet/freeze")]
    public async Task<IActionResult> FreezeWallet(
        int targetUserId,
        [FromQuery] int adminUserId,
        [FromQuery] string? reason)
    {
        var result = await _adminService.FreezeWallet(adminUserId, targetUserId, reason);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{targetUserId:int}/wallet/unfreeze")]
    public async Task<IActionResult> UnfreezeWallet(int targetUserId, [FromQuery] int adminUserId)
    {
        var result = await _adminService.UnfreezeWallet(adminUserId, targetUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("instruments")]
    public async Task<IActionResult> GetInstruments()
    {
        var result = await _adminService.GetInstruments();
        return Ok(result);
    }

    [HttpPost("instruments")]
    public async Task<IActionResult> AddInstrument([FromBody] CreateInstrumentRequestDto request)
    {
        var result = await _adminService.AddInstrument(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("instruments/{instrumentId:int}")]
    public async Task<IActionResult> UpdateInstrument(
        int instrumentId,
        [FromBody] UpdateInstrumentRequestDto request)
    {
        var result = await _adminService.UpdateInstrument(instrumentId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("instruments/{instrumentId:int}/deactivate")]
    public async Task<IActionResult> DeactivateInstrument(
        int instrumentId,
        [FromQuery] int adminUserId)
    {
        var result = await _adminService.DeactivateInstrument(adminUserId, instrumentId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("instruments/{instrumentId:int}/restore")]
    public async Task<IActionResult> RestoreInstrument(int instrumentId)
    {
        var result = await _adminService.RestoreInstrument(instrumentId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders()
    {
        var result = await _adminService.GetOrders();
        return Ok(result);
    }

    [HttpGet("users/{userId:int}/orders")]
    public async Task<IActionResult> GetUserOrders(int userId)
    {
        var result = await _adminService.GetUserOrders(userId);
        return Ok(result);
    }

    [HttpPost("orders/{orderId:int}/cancel")]
    public async Task<IActionResult> CancelUserOrder(int orderId, [FromQuery] int adminUserId)
    {
        var result = await _adminService.CancelUserOrder(adminUserId, orderId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("trades")]
    public async Task<IActionResult> GetTrades()
    {
        var result = await _adminService.GetTrades();
        return Ok(result);
    }

    [HttpGet("users/{userId:int}/trades")]
    public async Task<IActionResult> GetUserTrades(int userId)
    {
        var result = await _adminService.GetUserTrades(userId);
        return Ok(result);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var result = await _adminService.GetRoles();
        return Ok(result);
    }

    [HttpPost("roles")]
    public async Task<IActionResult> AddRole([FromBody] CreateRoleRequestDto request)
    {
        var result = await _adminService.AddRole(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("roles/{roleId:int}")]
    public async Task<IActionResult> UpdateRole(int roleId, [FromBody] CreateRoleRequestDto request)
    {
        var result = await _adminService.UpdateRole(roleId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("roles/{roleId:int}/deactivate")]
    public async Task<IActionResult> DeactivateRole(int roleId)
    {
        var result = await _adminService.DeactivateRole(roleId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("roles/{roleId:int}/restore")]
    public async Task<IActionResult> RestoreRole(int roleId)
    {
        var result = await _adminService.RestoreRole(roleId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencies()
    {
        var result = await _adminService.GetCurrencies();
        return Ok(result);
    }

    [HttpPost("currencies")]
    public async Task<IActionResult> AddCurrency([FromBody] CreateCurrencyRequestDto request)
    {
        var result = await _adminService.AddCurrency(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("currencies/{currencyId:int}")]
    public async Task<IActionResult> UpdateCurrency(int currencyId, [FromBody] CreateCurrencyRequestDto request)
    {
        var result = await _adminService.UpdateCurrency(currencyId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("currencies/{currencyId:int}/deactivate")]
    public async Task<IActionResult> DeactivateCurrency(int currencyId)
    {
        var result = await _adminService.DeactivateCurrency(currencyId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("currencies/{currencyId:int}/restore")]
    public async Task<IActionResult> RestoreCurrency(int currencyId)
    {
        var result = await _adminService.RestoreCurrency(currencyId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs()
    {
        var result = await _adminService.GetAuditLogs();
        return Ok(result);
    }
}
