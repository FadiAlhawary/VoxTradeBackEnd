using Microsoft.AspNetCore.Mvc;
using VoxTrade.PaymentMethods.DTOs;
using VoxTrade.PaymentMethods.Interfaces;

namespace VoxTrade.PaymentMethods.Controllers;

[ApiController]
[Route("api/payment-methods")]
public class PaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodService _paymentMethodService;

    public PaymentMethodsController(IPaymentMethodService paymentMethodService)
    {
        _paymentMethodService = paymentMethodService;
    }

    /// <summary>Catalog of payment types (card, bank, etc.) from payment_method table.</summary>
    [HttpGet("types")]
    public async Task<IActionResult> GetTypes()
    {
        var result = await _paymentMethodService.GetAvailableTypesAsync();
        return Ok(result);
    }

    [HttpGet("users/{userId:int}")]
    public async Task<IActionResult> GetUserPaymentMethods(int userId)
    {
        var result = await _paymentMethodService.GetUserPaymentMethodsAsync(userId);
        return Ok(result);
    }

    [HttpGet("users/{userId:int}/{userPaymentMethodId:int}")]
    public async Task<IActionResult> GetUserPaymentMethod(int userId, int userPaymentMethodId)
    {
        var result = await _paymentMethodService.GetUserPaymentMethodByIdAsync(userId, userPaymentMethodId);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    [HttpPost("users/{userId:int}")]
    public async Task<IActionResult> AddUserPaymentMethod(
        int userId,
        [FromBody] AddUserPaymentMethodRequestDto request)
    {
        var result = await _paymentMethodService.AddUserPaymentMethodAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("users/{userId:int}/{userPaymentMethodId:int}")]
    public async Task<IActionResult> UpdateUserPaymentMethod(
        int userId,
        int userPaymentMethodId,
        [FromBody] UpdateUserPaymentMethodRequestDto request)
    {
        var result = await _paymentMethodService.UpdateUserPaymentMethodAsync(
            userId,
            userPaymentMethodId,
            request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("users/{userId:int}/{userPaymentMethodId:int}")]
    public async Task<IActionResult> RemoveUserPaymentMethod(int userId, int userPaymentMethodId)
    {
        var result = await _paymentMethodService.RemoveUserPaymentMethodAsync(userId, userPaymentMethodId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
