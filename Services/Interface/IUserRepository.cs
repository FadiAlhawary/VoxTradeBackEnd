using VoxTrade.Models;
using VoxTrade.Models.DTO;

public interface IUserRepository
{
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User?> GetUserByIdAsync(int id);
    Task<User> CreateUserAsync(User user);
    Task<User> UpdateUserAsync(User user);
    Task<bool> UserExistsByUsernameAsync(string username);
    Task<bool> UserExistsByEmailAsync(string email);
    Task<UserDTO?> GetUserProfileById(int userId);
}
