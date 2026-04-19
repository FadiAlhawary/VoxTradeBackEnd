using Microsoft.AspNetCore.Mvc;
using VoxTrade.Models;
using VoxTrade.Models.Auth;
using VoxTrade.Models.DTO;
using VoxTrade.Services.Interface;

namespace VoxTrade.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IUserRepository userRepository,
        IEmailService emailService,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _userRepository = userRepository;
        _emailService = emailService;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
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
            UserDTO userDTO = new UserDTO();

            userDTO = await _userRepository.GetUserProfileById(user.Id);
            return Ok(new AuthResponse
            {
                Success = true,
                Message = "Registration successful",
                Token = token,
                User = userDTO
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

            UserDTO userDTO = new UserDTO();

            userDTO =await _userRepository.GetUserProfileById(user.Id);

            return Ok(new AuthResponse
            {
                Success = true,
                Message = "Login successful",
                Token = token,
                User = userDTO
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

    [HttpPut("backup-email")]
    public async Task<IActionResult> UpdateBackupEmail([FromBody] UpdateBackupEmailRequest request)
    {
        if (request.UserId <= 0)
        {
            return BadRequest(new { success = false, message = "Valid user id is required." });
        }

        if (string.IsNullOrWhiteSpace(request.BackupEmail) || !request.BackupEmail.Contains("@"))
        {
            return BadRequest(new { success = false, message = "Valid backup email is required." });
        }

        var user = await _userRepository.GetUserByIdAsync(request.UserId);
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found." });
        }

        await _userRepository.UpdateBackupEmailAsync(request.UserId, request.BackupEmail.Trim());
        return Ok(new { success = true, message = "Backup email updated successfully." });
    }

    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (request.UserId <= 0)
        {
            return BadRequest(new { success = false, message = "Valid user id is required." });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest(new { success = false, message = "Password must be at least 8 characters long." });
        }

        var user = await _userRepository.GetUserByIdAsync(request.UserId);
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found." });
        }

        var hashedPassword = _authService.HashPassword(request.NewPassword);
        var updated = await _userRepository.UpdatePasswordHashAsync(request.UserId, hashedPassword);
        if (!updated)
        {
            return StatusCode(500, new { success = false, message = "Could not update password." });
        }

        return Ok(new { success = true, message = "Password updated successfully." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { success = false, message = "Email is required." });
        }

        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user != null)
        {
            var token = _authService.GeneratePasswordResetToken(user);
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
            var resetLink = $"{frontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "SMTP is not configured. Forgot-password email was not sent.");

                if (_environment.IsDevelopment())
                {
                    return Ok(new
                    {
                        success = true,
                        message = "SMTP not configured. Use the development reset link below.",
                        resetLink
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reset password email.");
                return StatusCode(500, new { success = false, message = "Unable to send reset email right now. Please try again later." });
            }
        }

        // Always return success to avoid email enumeration.
        return Ok(new { success = true, message = "If an account exists for this email, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { success = false, message = "Reset token is required." });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest(new { success = false, message = "Password must be at least 8 characters long." });
        }

        var userId = _authService.ValidatePasswordResetToken(request.Token);
        if (!userId.HasValue)
        {
            return BadRequest(new { success = false, message = "Invalid or expired reset token." });
        }

        var user = await _userRepository.GetUserByIdAsync(userId.Value);
        if (user == null)
        {
            return NotFound(new { success = false, message = "User not found." });
        }

        var hashedPassword = _authService.HashPassword(request.NewPassword);
        var updated = await _userRepository.UpdatePasswordHashAsync(user.Id, hashedPassword);
        if (!updated)
        {
            return StatusCode(500, new { success = false, message = "Could not update password." });
        }

        return Ok(new { success = true, message = "Password reset successfully." });
    }
}
