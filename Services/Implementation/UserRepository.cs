using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;

public class UserRepository : IUserRepository
{
    private readonly TradingDbContext _context;

    public UserRepository(TradingDbContext context)
    {
        _context = context;
    }

    public async Task<User> GetUserByUsernameAsync(string username)
    {
        var user = await _context.Users
            .Include(u => u.ContactInfo)
            .FirstOrDefaultAsync(u => u.Username == username && (!u.IsDeleted.HasValue || !u.IsDeleted.Value));

        if (user != null)
        {
            user.Email = user.ContactInfo?.PrimaryEmail;
            user.PhoneNumber = user.ContactInfo?.PrimaryPhoneNumber;
        }

        return user;
    }

    public async Task<User> GetUserByIdAsync(int id)
    {
        var user = await _context.Users
            .Include(u => u.ContactInfo)
            .FirstOrDefaultAsync(u => u.Id == id && (!u.IsDeleted.HasValue || !u.IsDeleted.Value));

        if (user != null)
        {
            user.Email = user.ContactInfo?.PrimaryEmail;
            user.PhoneNumber = user.ContactInfo?.PrimaryPhoneNumber;
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
}
