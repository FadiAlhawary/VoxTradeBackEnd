using System.Data;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VoxTrade.Admin.DTOs;
using VoxTrade.Admin.Interfaces;
using VoxTrade.Api.Data;
using VoxTrade.Services;

namespace VoxTrade.Admin.Services;

public class AdminService : IAdminService
{
    private const int AdminRoleId = 1;

    private readonly TradingDbContext _context;
    private readonly ILogger<AdminService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AdminService(TradingDbContext context, ILogger<AdminService> logger)
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

    private async Task<bool> IsAdmin(int userId)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM public.users
            WHERE id = @UserId
              AND role_id = @AdminRoleId
              AND COALESCE(is_deleted, false) = false
              AND COALESCE(is_locked, false) = false;
            """;

        var connection = await GetOpenConnection();
        var count = await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, AdminRoleId });
        return count > 0;
    }

    private static AdminActionResponseDto Fail(string message) =>
        new() { Success = false, Message = message };

    private static AdminActionResponseDto Ok(string message, int? id = null) =>
        new() { Success = true, Message = message, Id = id };

    private AdminActionResponseDto? DeserializeDbResponse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Fail("Invalid database response");

        try
        {
            return JsonSerializer.Deserialize<AdminActionResponseDto>(json, JsonOptions)
                   ?? Fail("Invalid database response");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize database response: {Json}", json);
            return Fail("Invalid database response");
        }
    }

    private async Task<AdminActionResponseDto> RequireAdmin(int adminUserId)
    {
        if (!await IsAdmin(adminUserId))
            return Fail("Unauthorized: admin access required");
        return Ok(string.Empty);
    }

    private sealed class UserRowSnapshot
    {
        public int? RoleId { get; init; }
        public bool IsDeleted { get; init; }
        public bool IsLocked { get; init; }
    }

    private async Task<UserRowSnapshot?> GetUserRowSnapshot(IDbConnection connection, int userId)
    {
        return await connection.QueryFirstOrDefaultAsync<UserRowSnapshot>(
            """
            SELECT
                u.role_id AS RoleId,
                COALESCE(u.is_deleted, false) AS IsDeleted,
                COALESCE(u.is_locked, false) AS IsLocked
            FROM public.users u
            WHERE u.id = @UserId;
            """,
            new { UserId = userId });
    }

    private static AdminActionResponseDto? BlockPrimaryAdmin(UserRowSnapshot user, string action)
    {
        if (user.RoleId != AdminRoleId)
            return null;

        return action switch
        {
            "deactivate" => Fail("Cannot deactivate the primary admin account"),
            "lock" => Fail("Cannot lock the primary admin account"),
            _ => Fail($"Cannot {action} the primary admin account")
        };
    }

    private async Task<int> ExecuteUserDeactivateAsync(
        IDbConnection connection,
        int adminUserId,
        int targetUserId)
    {
        const string fullSql = """
            UPDATE public.users
            SET is_deleted = true,
                delete_at = NOW(),
                deleted_by = @AdminUserId
            WHERE id = @TargetUserId
              AND COALESCE(is_deleted, false) = false
              AND role_id IS DISTINCT FROM @AdminRoleId;
            """;

        const string minimalSql = """
            UPDATE public.users
            SET is_deleted = true
            WHERE id = @TargetUserId
              AND COALESCE(is_deleted, false) = false
              AND role_id IS DISTINCT FROM @AdminRoleId;
            """;

        var parameters = new
        {
            AdminUserId = adminUserId,
            TargetUserId = targetUserId,
            AdminRoleId
        };

        try
        {
            return await connection.ExecuteAsync(fullSql, parameters);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogWarning(
                ex,
                "User soft-delete audit columns missing; updating is_deleted only for user {TargetUserId}",
                targetUserId);
            return await connection.ExecuteAsync(minimalSql, parameters);
        }
    }

    private async Task<int> ExecuteUserRestoreAsync(IDbConnection connection, int targetUserId)
    {
        const string fullSql = """
            UPDATE public.users
            SET is_deleted = false,
                delete_at = NULL,
                deleted_by = NULL
            WHERE id = @TargetUserId;
            """;

        const string minimalSql = """
            UPDATE public.users
            SET is_deleted = false
            WHERE id = @TargetUserId;
            """;

        try
        {
            return await connection.ExecuteAsync(fullSql, new { TargetUserId = targetUserId });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogWarning(
                ex,
                "User soft-delete audit columns missing; clearing is_deleted only for user {TargetUserId}",
                targetUserId);
            return await connection.ExecuteAsync(minimalSql, new { TargetUserId = targetUserId });
        }
    }

    public async Task<AdminDashboardDto> GetDashboard()
    {
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM public.users) AS TotalUsers,
                (SELECT COUNT(*) FROM public.users
                 WHERE COALESCE(is_deleted, false) = false
                   AND COALESCE(is_locked, false) = false) AS ActiveUsers,
                (SELECT COUNT(*) FROM public.users
                 WHERE COALESCE(is_locked, false) = true) AS LockedUsers,
                (SELECT COUNT(*) FROM public.instruments) AS TotalInstruments,
                (SELECT COUNT(*) FROM public.instruments i
                 LEFT JOIN public.lookup ls ON ls.id = i.status
                 WHERE COALESCE(i.is_deleted, false) = false
                   AND ls.code = 'active') AS ActiveInstruments,
                (SELECT COUNT(*) FROM public.orders) AS TotalOrders,
                (SELECT COUNT(*) FROM public.orders o
                 INNER JOIN public.lookup st ON st.id = o.status_id
                 WHERE st.code = 'pending') AS PendingOrders,
                (SELECT COUNT(*) FROM public.orders o
                 INNER JOIN public.lookup st ON st.id = o.status_id
                 WHERE st.code = 'filled') AS FilledOrders,
                (SELECT COUNT(*) FROM public.orders o
                 INNER JOIN public.lookup st ON st.id = o.status_id
                 WHERE st.code = 'cancelled') AS CancelledOrders,
                (SELECT COUNT(*) FROM public.trades) AS TotalTrades,
                COALESCE((SELECT SUM(balance) FROM public.wallets), 0) AS TotalWalletBalance,
                COALESCE((SELECT SUM(available_balance) FROM public.wallets), 0) AS TotalAvailableBalance,
                COALESCE((SELECT SUM(reserved_balance) FROM public.wallets), 0) AS TotalReservedBalance;
            """;

        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QuerySingleAsync<AdminDashboardDto>(sql);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load admin dashboard");
            return new AdminDashboardDto();
        }
    }

    private const string AdminUserSelectSql = """
        SELECT
            u.id AS Id,
            u.username AS Username,
            u.first_name_en AS FirstNameEn,
            u.last_name_en AS LastNameEn,
            u.first_name_ar AS FirstNameAr,
            u.last_name_ar AS LastNameAr,
            u.role_id AS RoleId,
            r.role_name_en AS RoleName,
            COALESCE(u.is_deleted, false) AS IsDeleted,
            COALESCE(u.is_deleted, false) AS IsDeactivated,
            COALESCE(u.is_locked, false) AS IsLocked,
            u.lock_reason AS LockReason,
            COALESCE(u.is_logged_in, false) AS IsLoggedIn,
            COALESCE(u.is_2fa_enabled, false) AS Is2FaEnabled,
            u.primary_currency_id AS PrimaryCurrencyId,
            COALESCE(u.created_at, NOW()) AS CreatedAt,
            u.last_login_date AS LastLoginDate
        FROM public.users u
        LEFT JOIN public.roles r ON r.id = u.role_id
        """;

    private const string WalletsByUserIdsSql = """
        SELECT DISTINCT ON (w.user_id)
            w.id AS Id,
            w.user_id AS UserId,
            u.username AS Username,
            w.currency_id AS CurrencyId,
            c.symbol AS CurrencySymbol,
            w.balance AS Balance,
            w.available_balance AS AvailableBalance,
            w.reserved_balance AS ReservedBalance,
            w.status AS Status,
            w.freeze_reason AS FreezeReason,
            w.updated_at AS UpdatedAt
        FROM public.wallets w
        LEFT JOIN public.users u ON u.id = w.user_id
        LEFT JOIN public.currencies c ON c.id = w.currency_id
        WHERE w.user_id = ANY(@UserIds)
        ORDER BY w.user_id, w.id;
        """;

    private async Task AttachWalletsToUsers(IReadOnlyList<AdminUserDto> users)
    {
        if (users.Count == 0)
            return;

        try
        {
            var connection = await GetOpenConnection();
            var userIds = users.Select(u => u.Id).ToArray();
            var wallets = (await connection.QueryAsync<AdminWalletDto>(
                WalletsByUserIdsSql,
                new { UserIds = userIds })).ToList();

            var walletByUserId = wallets.ToDictionary(w => w.UserId);
            foreach (var user in users)
            {
                if (walletByUserId.TryGetValue(user.Id, out var wallet))
                    user.Wallet = wallet;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load wallets for admin user list");
        }
    }

    public async Task<List<AdminUserDto>> GetUsers()
    {
        try
        {
            var connection = await GetOpenConnection();
            var users = (await connection.QueryAsync<AdminUserDto>(
                AdminUserSelectSql + " ORDER BY u.id;")).ToList();

            await AttachWalletsToUsers(users);
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users");
            return new List<AdminUserDto>();
        }
    }

    public async Task<AdminUserDto?> GetUserById(int userId)
    {
        try
        {
            var connection = await GetOpenConnection();
            var user = await connection.QueryFirstOrDefaultAsync<AdminUserDto>(
                AdminUserSelectSql + " WHERE u.id = @UserId;",
                new { UserId = userId });

            if (user == null)
                return null;

            user.Wallet = await GetUserWallet(userId);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user {UserId}", userId);
            return null;
        }
    }

    public async Task<AdminActionResponseDto> UpdateUser(int adminUserId, int targetUserId, UpdateUserAdminRequestDto request)
    {
        var auth = await RequireAdmin(adminUserId);
        if (!auth.Success)
            return auth;

        var sets = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("TargetUserId", targetUserId);

        if (request.FirstNameEn != null)
        {
            sets.Add("first_name_en = @FirstNameEn");
            parameters.Add("FirstNameEn", request.FirstNameEn);
        }
        if (request.LastNameEn != null)
        {
            sets.Add("last_name_en = @LastNameEn");
            parameters.Add("LastNameEn", request.LastNameEn);
        }
        if (request.FirstNameAr != null)
        {
            sets.Add("first_name_ar = @FirstNameAr");
            parameters.Add("FirstNameAr", request.FirstNameAr);
        }
        if (request.LastNameAr != null)
        {
            sets.Add("last_name_ar = @LastNameAr");
            parameters.Add("LastNameAr", request.LastNameAr);
        }
        if (request.Username != null)
        {
            sets.Add("username = @Username");
            parameters.Add("Username", request.Username);
        }
        if (request.RoleId.HasValue)
        {
            sets.Add("role_id = @RoleId");
            parameters.Add("RoleId", request.RoleId.Value);
        }
        if (request.PrimaryCurrencyId.HasValue)
        {
            sets.Add("primary_currency_id = @PrimaryCurrencyId");
            parameters.Add("PrimaryCurrencyId", request.PrimaryCurrencyId.Value);
        }
        if (request.Is2FaEnabled.HasValue)
        {
            sets.Add("is_2fa_enabled = @Is2FaEnabled");
            parameters.Add("Is2FaEnabled", request.Is2FaEnabled.Value);
        }

        if (sets.Count == 0)
            return Fail("No fields to update");

        try
        {
            var connection = await GetOpenConnection();
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM public.users WHERE id = @TargetUserId",
                parameters);
            if (exists == 0)
                return Fail("User not found");

            var sql = $"""
                UPDATE public.users
                SET {string.Join(", ", sets)}
                WHERE id = @TargetUserId;
                """;

            await connection.ExecuteAsync(sql, parameters);
            return Ok("User updated successfully", targetUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user {TargetUserId}", targetUserId);
            return Fail("Failed to update user");
        }
    }

    public async Task<AdminActionResponseDto> ChangeUserRole(int adminUserId, int targetUserId, int roleId)
    {
        var auth = await RequireAdmin(adminUserId);
        if (!auth.Success)
            return auth;

        if (adminUserId == targetUserId)
            return Fail("Cannot change your own role");

        try
        {
            var connection = await GetOpenConnection();
            var target = await GetUserRowSnapshot(connection, targetUserId);
            if (target == null)
                return Fail("User not found");
            var block = BlockPrimaryAdmin(target, "change role of");
            if (block != null)
                return block;
            if (roleId == AdminRoleId)
                return Fail($"Cannot assign role id {AdminRoleId} through this endpoint");

            var roleExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM public.roles WHERE id = @RoleId AND COALESCE(is_deleted, false) = false",
                new { RoleId = roleId });
            if (roleExists == 0)
                return Fail("Role not found");

            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.users
                SET role_id = @RoleId
                WHERE id = @TargetUserId;
                """,
                new { RoleId = roleId, TargetUserId = targetUserId });

            if (rows == 0)
                return Fail("User not found");

            return Ok("User role updated successfully", targetUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change role for user {TargetUserId}", targetUserId);
            return Fail("Failed to change user role");
        }
    }

    public async Task<AdminActionResponseDto> DeactivateUser(int adminUserId, int targetUserId)
    {
        var auth = await RequireAdmin(adminUserId);
        if (!auth.Success)
            return auth;

        if (adminUserId == targetUserId)
            return Fail("Cannot deactivate your own account");

        try
        {
            var connection = await GetOpenConnection();
            var target = await GetUserRowSnapshot(connection, targetUserId);
            if (target == null)
                return Fail("User not found");
            var block = BlockPrimaryAdmin(target, "deactivate");
            if (block != null)
                return block;
            if (target.IsDeleted)
                return Fail("User is already deactivated");

            var rows = await ExecuteUserDeactivateAsync(connection, adminUserId, targetUserId);

            if (rows == 0)
                return Fail("User could not be deactivated");

            return Ok("User deactivated successfully", targetUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate user {TargetUserId}", targetUserId);
            return Fail("Failed to deactivate user");
        }
    }

    public async Task<AdminActionResponseDto> RestoreUser(int adminUserId, int targetUserId)
    {
        var auth = await RequireAdmin(adminUserId);
        if (!auth.Success)
            return auth;

        try
        {
            var connection = await GetOpenConnection();
            var target = await GetUserRowSnapshot(connection, targetUserId);
            if (target == null)
                return Fail("User not found");
            if (!target.IsDeleted)
                return Fail("User is not deactivated");

            var rows = await ExecuteUserRestoreAsync(connection, targetUserId);

            if (rows == 0)
                return Fail("User could not be restored");

            return Ok("User restored successfully", targetUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore user {TargetUserId}", targetUserId);
            return Fail("Failed to restore user");
        }
    }

    public async Task<AdminActionResponseDto> LockUser(int adminUserId, int targetUserId, string? reason)
    {
        var auth = await RequireAdmin(adminUserId);
        if (!auth.Success)
            return auth;

        if (adminUserId == targetUserId)
            return Fail("Cannot lock your own account");

        try
        {
            var connection = await GetOpenConnection();
            var target = await GetUserRowSnapshot(connection, targetUserId);
            if (target == null)
                return Fail("User not found");
            var block = BlockPrimaryAdmin(target, "lock");
            if (block != null)
                return block;
            if (target.IsLocked)
                return Fail("User is already locked");

            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.users
                SET is_locked = true,
                    locked_at = NOW(),
                    locked_by = @AdminUserId,
                    lock_reason = @Reason
                WHERE id = @TargetUserId
                  AND COALESCE(is_locked, false) = false
                  AND role_id IS DISTINCT FROM @AdminRoleId;
                """,
                new
                {
                    AdminUserId = adminUserId,
                    TargetUserId = targetUserId,
                    Reason = reason,
                    AdminRoleId
                });

            if (rows == 0)
                return Fail("User not found or already locked");

            return Ok("User locked successfully", targetUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lock user {TargetUserId}", targetUserId);
            return Fail("Failed to lock user");
        }
    }

    public async Task<AdminActionResponseDto> UnlockUser(int adminUserId, int targetUserId)
    {
        var auth = await RequireAdmin(adminUserId);
        if (!auth.Success)
            return auth;

        try
        {
            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.users
                SET is_locked = false,
                    locked_at = NULL,
                    locked_by = NULL,
                    lock_reason = NULL
                WHERE id = @TargetUserId;
                """,
                new { TargetUserId = targetUserId });

            if (rows == 0)
                return Fail("User not found");

            return Ok("User unlocked successfully", targetUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unlock user {TargetUserId}", targetUserId);
            return Fail("Failed to unlock user");
        }
    }

    public async Task<List<AdminWalletDto>> GetWallets()
    {
        const string sql = """
            SELECT
                w.id AS Id,
                w.user_id AS UserId,
                u.username AS Username,
                w.currency_id AS CurrencyId,
                c.symbol AS CurrencySymbol,
                w.balance AS Balance,
                w.available_balance AS AvailableBalance,
                w.reserved_balance AS ReservedBalance,
                w.status AS Status,
                w.freeze_reason AS FreezeReason,
                w.updated_at AS UpdatedAt
            FROM public.wallets w
            LEFT JOIN public.users u ON u.id = w.user_id
            LEFT JOIN public.currencies c ON c.id = w.currency_id
            ORDER BY w.id;
            """;

        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QueryAsync<AdminWalletDto>(sql);
            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get wallets");
            return new List<AdminWalletDto>();
        }
    }

    public async Task<AdminWalletDto?> GetUserWallet(int userId)
    {
        const string sql = """
            SELECT
                w.id AS Id,
                w.user_id AS UserId,
                u.username AS Username,
                w.currency_id AS CurrencyId,
                c.symbol AS CurrencySymbol,
                w.balance AS Balance,
                w.available_balance AS AvailableBalance,
                w.reserved_balance AS ReservedBalance,
                w.status AS Status,
                w.freeze_reason AS FreezeReason,
                w.updated_at AS UpdatedAt
            FROM public.wallets w
            LEFT JOIN public.users u ON u.id = w.user_id
            LEFT JOIN public.currencies c ON c.id = w.currency_id
            WHERE w.user_id = @UserId
            ORDER BY w.id
            LIMIT 1;
            """;

        try
        {
            var connection = await GetOpenConnection();
            return await connection.QueryFirstOrDefaultAsync<AdminWalletDto>(sql, new { UserId = userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get wallet for user {UserId}", userId);
            return null;
        }
    }

    public async Task<AdminActionResponseDto> AddFunds(AdjustWalletRequestDto request)
    {
        var auth = await RequireAdmin(request.AdminUserId);
        if (!auth.Success)
            return auth;

        if (request.Amount <= 0)
            return Fail("Amount must be greater than zero");

        return await AdjustWallet(request, "add");
    }

    public async Task<AdminActionResponseDto> DeductFunds(AdjustWalletRequestDto request)
    {
        var auth = await RequireAdmin(request.AdminUserId);
        if (!auth.Success)
            return auth;

        if (request.Amount <= 0)
            return Fail("Amount must be greater than zero");

        return await AdjustWallet(request, "deduct");
    }

    private async Task<AdminActionResponseDto> AdjustWallet(AdjustWalletRequestDto request, string operation)
    {
        try
        {
            var connection = await GetOpenConnection();
            var frozenMessage = await WalletFreezeGuard.GetFrozenMessageIfAnyAsync(
                connection,
                request.TargetUserId);
            if (frozenMessage != null)
                return Fail(frozenMessage);

            const string sql = """
                SELECT public.admin_adjust_wallet(
                    @AdminUserId,
                    @TargetUserId,
                    @Amount,
                    @Operation,
                    @Description
                )::text;
                """;

            var json = await connection.ExecuteScalarAsync<string>(
                sql,
                new
                {
                    request.AdminUserId,
                    request.TargetUserId,
                    request.Amount,
                    Operation = operation,
                    Description = request.Description ?? $"Admin {operation} funds"
                });

            return DeserializeDbResponse(json) ?? Fail("Invalid database response");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to {Operation} funds. AdminUserId={AdminUserId}, TargetUserId={TargetUserId}",
                operation,
                request.AdminUserId,
                request.TargetUserId);
            return Fail($"Failed to {operation} funds");
        }
    }

    public async Task<AdminActionResponseDto> FreezeWallet(int adminUserId, int targetUserId, string? reason)
    {
        var auth = await RequireAdmin(adminUserId);
        if (!auth.Success)
            return auth;

        try
        {
            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.wallets
                SET status = false,
                    frozen_at = NOW(),
                    frozen_by = @AdminUserId,
                    freeze_reason = @Reason,
                    updated_at = NOW()
                WHERE user_id = @TargetUserId;
                """,
                new { AdminUserId = adminUserId, TargetUserId = targetUserId, Reason = reason });

            if (rows == 0)
                return Fail("Wallet not found");

            return Ok("Wallet frozen successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to freeze wallet for user {TargetUserId}", targetUserId);
            return Fail("Failed to freeze wallet");
        }
    }

    public async Task<AdminActionResponseDto> UnfreezeWallet(int adminUserId, int targetUserId)
    {
        var auth = await RequireAdmin(adminUserId);
        if (!auth.Success)
            return auth;

        try
        {
            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.wallets
                SET status = true,
                    frozen_at = NULL,
                    frozen_by = NULL,
                    freeze_reason = NULL,
                    updated_at = NOW()
                WHERE user_id = @TargetUserId;
                """,
                new { TargetUserId = targetUserId });

            if (rows == 0)
                return Fail("Wallet not found");

            return Ok("Wallet unfrozen successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unfreeze wallet for user {TargetUserId}", targetUserId);
            return Fail("Failed to unfreeze wallet");
        }
    }

    public async Task<List<AdminInstrumentDto>> GetInstruments()
    {
        const string sql = """
            SELECT
                i.id AS Id,
                i.symbol AS Symbol,
                i.short_name AS ShortName,
                i.name AS Name,
                i.tick_size AS TickSize,
                i.min_quantity AS MinQuantity,
                i.instrument_type AS InstrumentType,
                i.status AS Status,
                COALESCE(i.is_deleted, false) AS IsDeleted,
                COALESCE(i.created_at, NOW()) AS CreatedAt
            FROM public.instruments i
            ORDER BY i.id;
            """;

        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QueryAsync<AdminInstrumentDto>(sql);
            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get instruments");
            return new List<AdminInstrumentDto>();
        }
    }

    public async Task<AdminActionResponseDto> AddInstrument(CreateInstrumentRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            return Fail("Symbol is required");

        try
        {
            const string sql = """
                INSERT INTO public.instruments (
                    symbol, short_name, name, tick_size, min_quantity,
                    instrument_type, status, created_at, is_deleted
                )
                VALUES (
                    @Symbol, @ShortName, @Name, @TickSize, @MinQuantity,
                    @InstrumentType, @Status, NOW(), false
                )
                RETURNING id;
                """;

            var connection = await GetOpenConnection();
            var id = await connection.ExecuteScalarAsync<int>(sql, request);
            return Ok("Instrument created successfully", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add instrument {Symbol}", request.Symbol);
            return Fail("Failed to add instrument");
        }
    }

    public async Task<AdminActionResponseDto> UpdateInstrument(int instrumentId, UpdateInstrumentRequestDto request)
    {
        var sets = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("InstrumentId", instrumentId);

        if (request.Symbol != null)
        {
            sets.Add("symbol = @Symbol");
            parameters.Add("Symbol", request.Symbol);
        }
        if (request.ShortName != null)
        {
            sets.Add("short_name = @ShortName");
            parameters.Add("ShortName", request.ShortName);
        }
        if (request.Name != null)
        {
            sets.Add("name = @Name");
            parameters.Add("Name", request.Name);
        }
        if (request.TickSize.HasValue)
        {
            sets.Add("tick_size = @TickSize");
            parameters.Add("TickSize", request.TickSize.Value);
        }
        if (request.MinQuantity.HasValue)
        {
            sets.Add("min_quantity = @MinQuantity");
            parameters.Add("MinQuantity", request.MinQuantity.Value);
        }
        if (request.InstrumentType.HasValue)
        {
            sets.Add("instrument_type = @InstrumentType");
            parameters.Add("InstrumentType", request.InstrumentType.Value);
        }
        if (request.Status.HasValue)
        {
            sets.Add("status = @Status");
            parameters.Add("Status", request.Status.Value);
        }

        if (sets.Count == 0)
            return Fail("No fields to update");

        try
        {
            var connection = await GetOpenConnection();
            var sql = $"""
                UPDATE public.instruments
                SET {string.Join(", ", sets)}
                WHERE id = @InstrumentId;
                """;

            var rows = await connection.ExecuteAsync(sql, parameters);
            if (rows == 0)
                return Fail("Instrument not found");

            return Ok("Instrument updated successfully", instrumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update instrument {InstrumentId}", instrumentId);
            return Fail("Failed to update instrument");
        }
    }

    public async Task<AdminActionResponseDto> DeactivateInstrument(int adminUserId, int instrumentId)
    {
        var auth = await RequireAdmin(adminUserId);
        if (!auth.Success)
            return auth;

        try
        {
            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.instruments
                SET is_deleted = true,
                    deleted_by = @AdminUserId,
                    deleted_at = NOW()
                WHERE id = @InstrumentId
                  AND COALESCE(is_deleted, false) = false;
                """,
                new { AdminUserId = adminUserId, InstrumentId = instrumentId });

            if (rows == 0)
                return Fail("Instrument not found or already deactivated");

            return Ok("Instrument deactivated successfully", instrumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate instrument {InstrumentId}", instrumentId);
            return Fail("Failed to deactivate instrument");
        }
    }

    public async Task<AdminActionResponseDto> RestoreInstrument(int instrumentId)
    {
        try
        {
            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.instruments
                SET is_deleted = false,
                    deleted_by = NULL,
                    deleted_at = NULL
                WHERE id = @InstrumentId;
                """,
                new { InstrumentId = instrumentId });

            if (rows == 0)
                return Fail("Instrument not found");

            return Ok("Instrument restored successfully", instrumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore instrument {InstrumentId}", instrumentId);
            return Fail("Failed to restore instrument");
        }
    }

    public async Task<List<AdminOrderDto>> GetOrders() => await QueryOrders(null);

    public async Task<List<AdminOrderDto>> GetUserOrders(int userId) => await QueryOrders(userId);

    private async Task<List<AdminOrderDto>> QueryOrders(int? userId)
    {
        var sql = new StringBuilder("""
            SELECT
                o.id AS Id,
                o.user_id AS UserId,
                u.username AS Username,
                o.instrument_id AS InstrumentId,
                i.symbol AS Symbol,
                o.side AS Side,
                o.quantity AS Quantity,
                o.limit_price AS LimitPrice,
                o.execution_price AS ExecutionPrice,
                COALESCE(o.filled_quantity, 0) AS FilledQuantity,
                o.remaining_quantity AS RemainingQuantity,
                st.code AS Status,
                ot.code AS OrderType,
                o.created_at AS CreatedAt
            FROM public.orders o
            LEFT JOIN public.users u ON u.id = o.user_id
            LEFT JOIN public.instruments i ON i.id = o.instrument_id
            LEFT JOIN public.lookup st ON st.id = o.status_id
            LEFT JOIN public.lookup ot ON ot.id = o.order_type_id
            """);

        if (userId.HasValue)
        {
            sql.Append(" WHERE o.user_id = @UserId");
        }

        sql.Append(" ORDER BY o.created_at DESC;");

        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QueryAsync<AdminOrderDto>(
                sql.ToString(),
                userId.HasValue ? new { UserId = userId.Value } : null);
            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get orders");
            return new List<AdminOrderDto>();
        }
    }

    public async Task<AdminActionResponseDto> CancelUserOrder(int adminUserId, int orderId)
    {
        var auth = await RequireAdmin(adminUserId);
        if (!auth.Success)
            return auth;

        try
        {
            var connection = await GetOpenConnection();
            var orderUserId = await connection.ExecuteScalarAsync<int?>(
                "SELECT user_id FROM public.orders WHERE id = @OrderId",
                new { OrderId = orderId });

            if (!orderUserId.HasValue)
                return Fail("Order not found");

            const string sql = """
                SELECT public.cancel_demo_order(@OrderId, @UserId)::text;
                """;

            var json = await connection.ExecuteScalarAsync<string>(
                sql,
                new { OrderId = orderId, UserId = orderUserId.Value });

            var result = DeserializeDbResponse(json) ?? Fail("Invalid database response");
            if (result.Success && !result.Id.HasValue)
                result.Id = orderId;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order {OrderId}", orderId);
            return Fail("Failed to cancel order");
        }
    }

    public async Task<List<AdminTradeDto>> GetTrades() => await QueryTrades(null);

    public async Task<List<AdminTradeDto>> GetUserTrades(int userId) => await QueryTrades(userId);

    private async Task<List<AdminTradeDto>> QueryTrades(int? userId)
    {
        var sql = new StringBuilder("""
            SELECT
                t.id AS Id,
                t.order_id AS OrderId,
                t.user_id AS UserId,
                u.username AS Username,
                t.instrument_id AS InstrumentId,
                i.symbol AS Symbol,
                t.side AS Side,
                t.price AS Price,
                t.quantity AS Quantity,
                t.trade_value AS TradeValue,
                t.executed_at AS ExecutedAt
            FROM public.trades t
            LEFT JOIN public.users u ON u.id = t.user_id
            LEFT JOIN public.instruments i ON i.id = t.instrument_id
            """);

        if (userId.HasValue)
            sql.Append(" WHERE t.user_id = @UserId");

        sql.Append(" ORDER BY t.executed_at DESC;");

        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QueryAsync<AdminTradeDto>(
                sql.ToString(),
                userId.HasValue ? new { UserId = userId.Value } : null);
            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trades");
            return new List<AdminTradeDto>();
        }
    }

    public async Task<List<AdminRoleDto>> GetRoles()
    {
        const string sql = """
            SELECT
                id AS Id,
                role_name_en AS RoleNameEn,
                role_name_ar AS RoleNameAr,
                description_en AS DescriptionEn,
                description_ar AS DescriptionAr,
                COALESCE(allow_delete, false) AS AllowDelete,
                COALESCE(allow_create, false) AS AllowCreate,
                COALESCE(allow_edit, false) AS AllowEdit,
                COALESCE(allow_super_view, false) AS AllowSuperView,
                COALESCE(lock_all_user, false) AS LockAllUser,
                COALESCE(is_deleted, false) AS IsDeleted
            FROM public.roles
            ORDER BY id;
            """;

        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QueryAsync<AdminRoleDto>(sql);
            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get roles");
            return new List<AdminRoleDto>();
        }
    }

    public async Task<AdminActionResponseDto> AddRole(CreateRoleRequestDto request)
    {
        try
        {
            const string sql = """
                INSERT INTO public.roles (
                    role_name_en, role_name_ar, description_en, description_ar,
                    allow_delete, allow_create, allow_edit, allow_super_view,
                    lock_all_user, is_deleted
                )
                VALUES (
                    @RoleNameEn, @RoleNameAr, @DescriptionEn, @DescriptionAr,
                    @AllowDelete, @AllowCreate, @AllowEdit, @AllowSuperView,
                    @LockAllUser, false
                )
                RETURNING id;
                """;

            var connection = await GetOpenConnection();
            var id = await connection.ExecuteScalarAsync<int>(sql, request);
            return Ok("Role created successfully", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add role");
            return Fail("Failed to add role");
        }
    }

    public async Task<AdminActionResponseDto> UpdateRole(int roleId, CreateRoleRequestDto request)
    {
        if (roleId == AdminRoleId)
            return Fail("Cannot modify the admin role");

        try
        {
            const string sql = """
                UPDATE public.roles
                SET role_name_en = @RoleNameEn,
                    role_name_ar = @RoleNameAr,
                    description_en = @DescriptionEn,
                    description_ar = @DescriptionAr,
                    allow_delete = @AllowDelete,
                    allow_create = @AllowCreate,
                    allow_edit = @AllowEdit,
                    allow_super_view = @AllowSuperView,
                    lock_all_user = @LockAllUser
                WHERE id = @RoleId;
                """;

            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(sql, new { RoleId = roleId, request.RoleNameEn, request.RoleNameAr, request.DescriptionEn, request.DescriptionAr, request.AllowDelete, request.AllowCreate, request.AllowEdit, request.AllowSuperView, request.LockAllUser });

            if (rows == 0)
                return Fail("Role not found");

            return Ok("Role updated successfully", roleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update role {RoleId}", roleId);
            return Fail("Failed to update role");
        }
    }

    public async Task<AdminActionResponseDto> DeactivateRole(int roleId)
    {
        if (roleId == AdminRoleId)
            return Fail("Cannot deactivate the admin role");

        try
        {
            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.roles
                SET is_deleted = true
                WHERE id = @RoleId
                  AND COALESCE(is_deleted, false) = false;
                """,
                new { RoleId = roleId });

            if (rows == 0)
                return Fail("Role not found or already deactivated");

            return Ok("Role deactivated successfully", roleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate role {RoleId}", roleId);
            return Fail("Failed to deactivate role");
        }
    }

    public async Task<AdminActionResponseDto> RestoreRole(int roleId)
    {
        try
        {
            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.roles
                SET is_deleted = false
                WHERE id = @RoleId;
                """,
                new { RoleId = roleId });

            if (rows == 0)
                return Fail("Role not found");

            return Ok("Role restored successfully", roleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore role {RoleId}", roleId);
            return Fail("Failed to restore role");
        }
    }

    public async Task<List<AdminCurrencyDto>> GetCurrencies()
    {
        const string sql = """
            SELECT
                id AS Id,
                name_en AS NameEn,
                name_ar AS NameAr,
                symbol AS Symbol,
                usd_rate AS UsdRate,
                COALESCE(is_deleted, false) AS IsDeleted
            FROM public.currencies
            ORDER BY id;
            """;

        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QueryAsync<AdminCurrencyDto>(sql);
            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get currencies");
            return new List<AdminCurrencyDto>();
        }
    }

    public async Task<AdminActionResponseDto> AddCurrency(CreateCurrencyRequestDto request)
    {
        try
        {
            const string sql = """
                INSERT INTO public.currencies (name_en, name_ar, symbol, usd_rate, created_at, is_deleted)
                VALUES (@NameEn, @NameAr, @Symbol, @UsdRate, NOW(), false)
                RETURNING id;
                """;

            var connection = await GetOpenConnection();
            var id = await connection.ExecuteScalarAsync<int>(sql, request);
            return Ok("Currency created successfully", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add currency");
            return Fail("Failed to add currency");
        }
    }

    public async Task<AdminActionResponseDto> UpdateCurrency(int currencyId, CreateCurrencyRequestDto request)
    {
        try
        {
            const string sql = """
                UPDATE public.currencies
                SET name_en = @NameEn,
                    name_ar = @NameAr,
                    symbol = @Symbol,
                    usd_rate = @UsdRate
                WHERE id = @CurrencyId;
                """;

            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(sql, new { CurrencyId = currencyId, request.NameEn, request.NameAr, request.Symbol, request.UsdRate });

            if (rows == 0)
                return Fail("Currency not found");

            return Ok("Currency updated successfully", currencyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update currency {CurrencyId}", currencyId);
            return Fail("Failed to update currency");
        }
    }

    public async Task<AdminActionResponseDto> DeactivateCurrency(int currencyId)
    {
        try
        {
            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.currencies
                SET is_deleted = true
                WHERE id = @CurrencyId
                  AND COALESCE(is_deleted, false) = false;
                """,
                new { CurrencyId = currencyId });

            if (rows == 0)
                return Fail("Currency not found or already deactivated");

            return Ok("Currency deactivated successfully", currencyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate currency {CurrencyId}", currencyId);
            return Fail("Failed to deactivate currency");
        }
    }

    public async Task<AdminActionResponseDto> RestoreCurrency(int currencyId)
    {
        try
        {
            var connection = await GetOpenConnection();
            var rows = await connection.ExecuteAsync(
                """
                UPDATE public.currencies
                SET is_deleted = false
                WHERE id = @CurrencyId;
                """,
                new { CurrencyId = currencyId });

            if (rows == 0)
                return Fail("Currency not found");

            return Ok("Currency restored successfully", currencyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore currency {CurrencyId}", currencyId);
            return Fail("Failed to restore currency");
        }
    }

    public async Task<List<AdminAuditLogDto>> GetAuditLogs()
    {
        const string sql = """
            SELECT
                a.id AS Id,
                a.user_id AS UserId,
                u.username AS Username,
                a.entity AS Entity,
                a.entity_id AS EntityId,
                a.action_code AS ActionCode,
                a.description AS Description,
                a.created_at AS CreatedAt
            FROM public.user_audit_logs a
            LEFT JOIN public.users u ON u.id = a.user_id
            ORDER BY a.created_at DESC
            LIMIT 300;
            """;

        try
        {
            var connection = await GetOpenConnection();
            var result = await connection.QueryAsync<AdminAuditLogDto>(sql);
            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit logs");
            return new List<AdminAuditLogDto>();
        }
    }
}
