using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.PaymentMethods.DTOs;
using VoxTrade.PaymentMethods.Interfaces;

namespace VoxTrade.PaymentMethods.Services;

public class PaymentMethodService : IPaymentMethodService
{
    private readonly TradingDbContext _context;
    private readonly ILogger<PaymentMethodService> _logger;

    public PaymentMethodService(TradingDbContext context, ILogger<PaymentMethodService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private async Task<IDbConnection> GetOpenConnection()
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();
        return connection;
    }

    private static PaymentMethodActionResponseDto Fail(string message) =>
        new() { Success = false, Message = message };

    private static PaymentMethodActionResponseDto Ok(string message, int? id = null, UserPaymentMethodDto? item = null) =>
        new() { Success = true, Message = message, Id = id, PaymentMethod = item };

    private async Task<bool> IsActiveUserAsync(IDbConnection connection, int userId)
    {
        var count = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM public.users
            WHERE id = @UserId
              AND COALESCE(is_deleted, false) = false
              AND COALESCE(is_locked, false) = false;
            """,
            new { UserId = userId });
        return count > 0;
    }

    public async Task<List<PaymentMethodTypeDto>> GetAvailableTypesAsync()
    {
        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QueryAsync<PaymentMethodTypeDto>(
                """
                SELECT
                    pm.id AS Id,
                    pm.mehtod_name AS MethodName,
                    pm.method_type AS MethodType
                FROM public.payment_method pm
                WHERE COALESCE(pm.is_deleted, false) = false
                ORDER BY pm.mehtod_name;
                """);

            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get payment method types");
            return new List<PaymentMethodTypeDto>();
        }
    }

    public async Task<List<UserPaymentMethodDto>> GetUserPaymentMethodsAsync(int userId)
    {
        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QueryAsync<UserPaymentMethodDto>(
                """
                SELECT
                    upm.id AS Id,
                    upm.user_id AS UserId,
                    upm.payment_method_id AS PaymentMethodId,
                    pm.mehtod_name AS MethodName,
                    pm.method_type AS MethodType,
                    upm.attribute_value_1 AS AttributeValue1,
                    upm.attribute_value_2 AS AttributeValue2,
                    upm.created_at AS CreatedAt
                FROM public.user_payment_methods upm
                INNER JOIN public.payment_method pm ON pm.id = upm.payment_method_id
                WHERE upm.user_id = @UserId
                  AND COALESCE(upm.is_deleted, false) = false
                  AND COALESCE(pm.is_deleted, false) = false
                ORDER BY upm.created_at DESC;
                """,
                new { UserId = userId });

            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get payment methods for user {UserId}", userId);
            return new List<UserPaymentMethodDto>();
        }
    }

    public async Task<UserPaymentMethodDto?> GetUserPaymentMethodByIdAsync(int userId, int userPaymentMethodId)
    {
        try
        {
            var connection = await GetOpenConnection();
            return await connection.QueryFirstOrDefaultAsync<UserPaymentMethodDto>(
                """
                SELECT
                    upm.id AS Id,
                    upm.user_id AS UserId,
                    upm.payment_method_id AS PaymentMethodId,
                    pm.mehtod_name AS MethodName,
                    pm.method_type AS MethodType,
                    upm.attribute_value_1 AS AttributeValue1,
                    upm.attribute_value_2 AS AttributeValue2,
                    upm.created_at AS CreatedAt
                FROM public.user_payment_methods upm
                INNER JOIN public.payment_method pm ON pm.id = upm.payment_method_id
                WHERE upm.id = @UserPaymentMethodId
                  AND upm.user_id = @UserId
                  AND COALESCE(upm.is_deleted, false) = false;
                """,
                new { UserId = userId, UserPaymentMethodId = userPaymentMethodId });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get payment method {UserPaymentMethodId} for user {UserId}",
                userPaymentMethodId,
                userId);
            return null;
        }
    }

    public async Task<PaymentMethodActionResponseDto> AddUserPaymentMethodAsync(
        int userId,
        AddUserPaymentMethodRequestDto request)
    {
        if (request.PaymentMethodId <= 0)
            return Fail("Payment method type is required.");

        if (string.IsNullOrWhiteSpace(request.AttributeValue1))
            return Fail("Primary payment detail is required (attributeValue1).");

        try
        {
            var connection = await GetOpenConnection();

            if (!await IsActiveUserAsync(connection, userId))
                return Fail("User not found or account is not active.");

            var typeExists = await connection.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(1)
                FROM public.payment_method
                WHERE id = @PaymentMethodId
                  AND COALESCE(is_deleted, false) = false;
                """,
                new { request.PaymentMethodId });

            if (typeExists == 0)
                return Fail("Payment method type not found.");

            var duplicate = await connection.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(1)
                FROM public.user_payment_methods
                WHERE user_id = @UserId
                  AND payment_method_id = @PaymentMethodId
                  AND COALESCE(is_deleted, false) = false
                  AND attribute_value_1 = @AttributeValue1;
                """,
                new
                {
                    UserId = userId,
                    request.PaymentMethodId,
                    AttributeValue1 = request.AttributeValue1.Trim()
                });

            if (duplicate > 0)
                return Fail("This payment method with the same details is already registered.");

            var newId = await connection.ExecuteScalarAsync<int>(
                """
                INSERT INTO public.user_payment_methods (
                    user_id,
                    payment_method_id,
                    attribute_value_1,
                    attribute_value_2,
                    created_at,
                    is_deleted
                )
                VALUES (
                    @UserId,
                    @PaymentMethodId,
                    @AttributeValue1,
                    @AttributeValue2,
                    NOW(),
                    false
                )
                RETURNING id;
                """,
                new
                {
                    UserId = userId,
                    request.PaymentMethodId,
                    AttributeValue1 = request.AttributeValue1.Trim(),
                    AttributeValue2 = string.IsNullOrWhiteSpace(request.AttributeValue2)
                        ? null
                        : request.AttributeValue2.Trim()
                });

            var created = await GetUserPaymentMethodByIdAsync(userId, newId);
            return Ok("Payment method added successfully.", newId, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add payment method for user {UserId}", userId);
            return Fail("Failed to add payment method.");
        }
    }

    public async Task<PaymentMethodActionResponseDto> UpdateUserPaymentMethodAsync(
        int userId,
        int userPaymentMethodId,
        UpdateUserPaymentMethodRequestDto request)
    {
        if (request.AttributeValue1 == null && request.AttributeValue2 == null)
            return Fail("No fields to update.");

        try
        {
            var connection = await GetOpenConnection();
            var existing = await GetUserPaymentMethodByIdAsync(userId, userPaymentMethodId);
            if (existing == null)
                return Fail("Payment method not found.");

            var attr1 = request.AttributeValue1?.Trim() ?? existing.AttributeValue1;
            var attr2 = request.AttributeValue2?.Trim() ?? existing.AttributeValue2;

            if (string.IsNullOrWhiteSpace(attr1))
                return Fail("Primary payment detail cannot be empty.");

            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.user_payment_methods
                SET attribute_value_1 = @AttributeValue1,
                    attribute_value_2 = @AttributeValue2
                WHERE id = @UserPaymentMethodId
                  AND user_id = @UserId
                  AND COALESCE(is_deleted, false) = false;
                """,
                new
                {
                    UserId = userId,
                    UserPaymentMethodId = userPaymentMethodId,
                    AttributeValue1 = attr1,
                    AttributeValue2 = attr2
                });

            if (rows == 0)
                return Fail("Payment method not found.");

            var updated = await GetUserPaymentMethodByIdAsync(userId, userPaymentMethodId);
            return Ok("Payment method updated successfully.", userPaymentMethodId, updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update payment method {UserPaymentMethodId} for user {UserId}",
                userPaymentMethodId,
                userId);
            return Fail("Failed to update payment method.");
        }
    }

    public async Task<PaymentMethodActionResponseDto> RemoveUserPaymentMethodAsync(
        int userId,
        int userPaymentMethodId)
    {
        try
        {
            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.user_payment_methods
                SET is_deleted = true
                WHERE id = @UserPaymentMethodId
                  AND user_id = @UserId
                  AND COALESCE(is_deleted, false) = false;
                """,
                new { UserId = userId, UserPaymentMethodId = userPaymentMethodId });

            if (rows == 0)
                return Fail("Payment method not found.");

            return Ok("Payment method removed successfully.", userPaymentMethodId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to remove payment method {UserPaymentMethodId} for user {UserId}",
                userPaymentMethodId,
                userId);
            return Fail("Failed to remove payment method.");
        }
    }
}
