using Microsoft.AspNetCore.Mvc;
using VoxTrade.Models;
using VoxTrade.Models.Auth;
using VoxTrade.Services.Interface;

namespace VoxTrade.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;

    public AuthController(IAuthService authService, IUserRepository userRepository)
    {
        _authService = authService;
        _userRepository = userRepository;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Username and password are required" 
                });

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Email is required" 
                });

            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Phone number is required" 
                });

            // Check if user already exists by username
            if (await _userRepository.UserExistsByUsernameAsync(request.Username))
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Username already exists" 
                });

            // Check if user already exists by email
            if (await _userRepository.UserExistsByEmailAsync(request.Email))
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Email already registered" 
                });

            // Create new user
            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                FirstNameEn = request.FirstNameEn,
                LastNameEn = request.LastNameEn,
                FirstNameAr = "", // Default empty string for Arabic fields
                LastNameAr = "",  // Default empty string for Arabic fields
                Dob = request.Dob?.ToUniversalTime(), // Convert to UTC
                Password = _authService.HashPassword(request.Password),
                PhoneNumber = request.PhoneNumber, // Store phone number to pass to CreateUserAsync
                CreatedAt = DateTime.UtcNow,
                IsLoggedIn = true,
                IsDeleted = false
            };

            

            // Save to database
            var createdUser = await _userRepository.CreateUserAsync(user);

            // Generate JWT token
            var token = _authService.GenerateJwtToken(createdUser);
            createdUser.Token = token;
            
            // Update token in database
            await _userRepository.UpdateUserAsync(createdUser);

            return Ok(new AuthResponse
            {
                Success = true,
                Message = "Registration successful",
                Token = token,
                User = new UserDto
                {
                    Id = createdUser.Id,
                    Username = createdUser.Username,
                    FirstNameEn = createdUser.FirstNameEn,
                    LastNameEn = createdUser.LastNameEn,
                    Email = createdUser.Email,
                    Dob = createdUser.Dob
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new AuthResponse 
            { 
                Success = false, 
                Message = $"Registration failed: {ex.Message}" 
            });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Username and password are required" 
                });

            // Find user by username in database
            var user = await _userRepository.GetUserByUsernameAsync(request.Username);
            if (user == null)
                return Unauthorized(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Invalid username or password" 
                });

            // Verify password
            if (!_authService.VerifyPassword(request.Password, user.Password))
                return Unauthorized(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Invalid username or password" 
                });

            // Generate JWT token
            var token = _authService.GenerateJwtToken(user);
            
            // Update login status in database (don't store token)
            user.IsLoggedIn = true;
            user.LastLoginDate = DateTime.UtcNow;

            // Update user in database
            await _userRepository.UpdateUserAsync(user);

            return Ok(new AuthResponse
            {
                Success = true,
                Message = "Login successful",
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    FirstNameEn = user.FirstNameEn,
                    LastNameEn = user.LastNameEn,
                    Email = user.Email,
                    Dob = user.Dob
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new AuthResponse 
            { 
                Success = false, 
                Message = $"Login failed: {ex.Message}" 
            });
        }
    }
}
