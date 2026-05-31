using Dapper;
using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.Models;
using VoxTrade.Models.Auth;
using VoxTrade.Models.DTO;

public class UserRepository : IUserRepository
{
    private readonly TradingDbContext _context;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(TradingDbContext context , ILogger<UserRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        var user = await _context.Users
            .Include(u => u.ContactInfo)
            .FirstOrDefaultAsync(u => u.Username == username && (!u.IsDeleted.HasValue || !u.IsDeleted.Value));

        if (user != null)
        {
            user.Email = user.ContactInfo?.PrimaryEmail ?? string.Empty;
            user.PhoneNumber = user.ContactInfo?.PrimaryPhoneNumber ?? string.Empty;
        }

        return user;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        var user = await _context.Users
            .Include(u => u.ContactInfo)
            .FirstOrDefaultAsync(u => u.Id == id && (!u.IsDeleted.HasValue || !u.IsDeleted.Value));

        if (user != null)
        {
            user.Email = user.ContactInfo?.PrimaryEmail ?? string.Empty;
            user.PhoneNumber = user.ContactInfo?.PrimaryPhoneNumber ?? string.Empty;
        }

        return user;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .Include(u => u.ContactInfo)
            .FirstOrDefaultAsync(u =>
                u.ContactInfo != null
                && u.ContactInfo.PrimaryEmail != null
                && u.ContactInfo.PrimaryEmail.ToLower() == normalizedEmail
                && (!u.IsDeleted.HasValue || !u.IsDeleted.Value)
            );

        if (user != null)
        {
            user.Email = user.ContactInfo?.PrimaryEmail ?? string.Empty;
            user.PhoneNumber = user.ContactInfo?.PrimaryPhoneNumber ?? string.Empty;
        }

        return user;
    }

    public async Task<User> CreateUserAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(user.Email) || !string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            var contactInfo = new ContactInfo
            {
                UserId = user.Id,
                PrimaryEmail = user.Email,
                PrimaryPhoneNumber = user.PhoneNumber,
                IsPrimaryEmailActive = !string.IsNullOrWhiteSpace(user.Email),
                IsPrimaryPhoneActive = !string.IsNullOrWhiteSpace(user.PhoneNumber),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.ContactInfo.Add(contactInfo);
            await _context.SaveChangesAsync();
        }

        return user;
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        // Only update specific fields to avoid issues with unmapped properties
        var existingUser = await _context.Users.FindAsync(user.Id);
        if (existingUser != null)
        {
            existingUser.IsLoggedIn = user.IsLoggedIn;
            existingUser.LastLoginDate = user.LastLoginDate;
            // Don't update Token as it's not mapped to database
            await _context.SaveChangesAsync();
        }

        if (!string.IsNullOrWhiteSpace(user.Email) || !string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            var contactInfo = await _context.ContactInfo.FirstOrDefaultAsync(ci => ci.UserId == user.Id && (ci.IsDeleted == null || ci.IsDeleted == false));
            if (contactInfo != null)
            {
                contactInfo.PrimaryEmail = user.Email;
                contactInfo.PrimaryPhoneNumber = user.PhoneNumber ?? contactInfo.PrimaryPhoneNumber;
                contactInfo.IsPrimaryEmailActive = !string.IsNullOrWhiteSpace(user.Email);
                contactInfo.IsPrimaryPhoneActive = !string.IsNullOrWhiteSpace(user.PhoneNumber);
                _context.ContactInfo.Update(contactInfo);
            }
            else
            {
                _context.ContactInfo.Add(new ContactInfo
                {
                    UserId = user.Id,
                    PrimaryEmail = user.Email,
                    PrimaryPhoneNumber = user.PhoneNumber ?? string.Empty,
                    IsPrimaryEmailActive = !string.IsNullOrWhiteSpace(user.Email),
                    IsPrimaryPhoneActive = !string.IsNullOrWhiteSpace(user.PhoneNumber),
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                });
            }

            await _context.SaveChangesAsync();
        }

        return user;
    }

    public async Task<bool> UserExistsByUsernameAsync(string username)
    {
        return await _context.Users.AnyAsync(u => u.Username == username && (!u.IsDeleted.HasValue || !u.IsDeleted.Value));
    }

    public async Task<bool> UserExistsByEmailAsync(string email)
    {
        return await _context.ContactInfo.AnyAsync(ci => ci.PrimaryEmail == email && (ci.IsDeleted == null || ci.IsDeleted == false));
    }
    public async Task<UserDTO?> GetUserProfileById(int userId)
    {
        try
        {
            const string sql = """
            SELECT
                u.id,
                u.first_name_en AS FirstNameEn,
                u.last_name_en AS LastNameEn,
                u.username,
                u.dob,
                ci.primary_email AS PrimaryEmail,
                ci.alt_email AS AltEmail,
                ci.primary_phone_number AS PrimaryPhoneNumber,
                ci.alt_phone_number AS AltPhoneNumber,
                COALESCE(ci.is_primary_email_active, false) AS IsPrimaryEmailActive,
                COALESCE(ci.is_alt_email_active, false) AS IsAltEmailActive,
                COALESCE(ci.is_primary_phone_number_active, false) AS IsPrimaryPhoneNumberActive,
                COALESCE(ci.is_alt_phone_number_active, false) AS IsAltPhoneNumberActive,
                u.role_id AS RoleId,
                r.role_name_en AS RoleNameEn,
                COALESCE(u.is_locked, false) AS IsLocked,
                COALESCE(u.is_deleted, false) AS IsDeleted
            FROM public.users u
            LEFT JOIN public.contact_info ci
                ON ci.user_id = u.id
               AND COALESCE(ci.is_deleted, false) = false
            LEFT JOIN public.roles r
                ON r.id = u.role_id
               AND COALESCE(r.is_deleted, false) = false
            WHERE u.id = @UserId;
            """;

            var connection = _context.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var result = await connection.QueryFirstOrDefaultAsync<UserDTO>(
                sql,
                new { UserId = userId });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user profile for user id {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> UpdateBackupEmailAsync(int userId, string backupEmail)
    {
        var contactInfo = await _context.ContactInfo
            .FirstOrDefaultAsync(ci => ci.UserId == userId && (ci.IsDeleted == null || ci.IsDeleted == false));

        if (contactInfo == null)
        {
            _context.ContactInfo.Add(new ContactInfo
            {
                UserId = userId,
                AltEmail = backupEmail,
                IsAltEmailActive = true,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
            });
        }
        else
        {
            contactInfo.AltEmail = backupEmail;
            contactInfo.IsAltEmailActive = true;
            _context.ContactInfo.Update(contactInfo);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdatePasswordHashAsync(int userId, string passwordHash)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && (!u.IsDeleted.HasValue || !u.IsDeleted.Value));

        if (user == null)
        {
            return false;
        }

        user.Password = passwordHash;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<UserSearchResultDto>> SearchUsersAsync(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<UserSearchResultDto>();
        }

        var trimmed = query.Trim();
        var pattern = $"%{trimmed}%";
        var cappedLimit = Math.Clamp(limit, 1, 50);

        try
        {
            const string sql = """
                SELECT
                    u.id AS Id,
                    u.username AS Username,
                    u.first_name_en AS FirstNameEn,
                    u.last_name_en AS LastNameEn,
                    TRIM(CONCAT(u.first_name_en, ' ', u.last_name_en)) AS DisplayName
                FROM public.users u
                WHERE COALESCE(u.is_deleted, false) = false
                  AND (
                        u.username ILIKE @Pattern
                     OR u.first_name_en ILIKE @Pattern
                     OR u.last_name_en ILIKE @Pattern
                     OR TRIM(CONCAT(u.first_name_en, ' ', u.last_name_en)) ILIKE @Pattern
                  )
                ORDER BY
                    CASE WHEN u.username ILIKE @Pattern THEN 0 ELSE 1 END,
                    u.username
                LIMIT @Limit;
                """;

            var connection = _context.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var results = await connection.QueryAsync<UserSearchResultDto>(
                sql,
                new { Pattern = pattern, Limit = cappedLimit });

            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search users with query {Query}", query);
            throw;
        }
    }
}
