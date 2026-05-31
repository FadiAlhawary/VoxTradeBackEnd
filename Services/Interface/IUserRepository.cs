using VoxTrade.Models;
using VoxTrade.Models.DTO;

public interface IUserRepository
{
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByIdAsync(int id);
    Task<User> CreateUserAsync(User user);
    Task<User> UpdateUserAsync(User user);
    Task<bool> UserExistsByUsernameAsync(string username);
    Task<bool> UserExistsByEmailAsync(string email);
    Task<UserDTO?> GetUserProfileById(int userId);
    Task<bool> UpdateBackupEmailAsync(int userId, string backupEmail);
    Task<bool> UpdatePasswordHashAsync(int userId, string passwordHash);
    Task<bool> UpdatePhoneNumberAsync(int userId, string phoneNumber);
    Task<bool> IsTwoFaEnabledAsync(int userId);
    Task SetTwoFaEnabledAsync(int userId, bool enabled);
    Task<List<UserSearchResultDto>> SearchUsersAsync(string query, int limit = 20);
}
