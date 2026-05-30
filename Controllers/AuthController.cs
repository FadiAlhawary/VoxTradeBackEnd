using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;

    private static string OtpCacheKey(int userId) => $"2fa_otp:{userId}";

    public AuthController(
        IAuthService authService,
        IUserRepository userRepository,
        IEmailService emailService,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<AuthController> logger,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory)
    {
        _authService = authService;
        _userRepository = userRepository;
        _emailService = emailService;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("google-register")]
    public async Task<ActionResult<AuthResponse>> GoogleRegister([FromBody] GoogleRegisterRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.IdToken))
                return BadRequest(new AuthResponse { Success = false, Message = "ID token is required." });

            // Validate token with Google
            var httpClient = _httpClientFactory.CreateClient();
            var tokenInfoUrl = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(request.IdToken)}";
            var googleResponse = await httpClient.GetAsync(tokenInfoUrl);

            if (!googleResponse.IsSuccessStatusCode)
                return Unauthorized(new AuthResponse { Success = false, Message = "Invalid Google token." });

            var json = await googleResponse.Content.ReadAsStringAsync();
            var tokenInfo = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (tokenInfo == null || !tokenInfo.TryGetValue("email", out var email) || string.IsNullOrEmpty(email))
                return Unauthorized(new AuthResponse { Success = false, Message = "Could not read email from Google token." });

            // Verify audience
            var expectedClientId = _configuration["GoogleAuth:ClientId"];
            if (!string.IsNullOrEmpty(expectedClientId)
                && tokenInfo.TryGetValue("aud", out var aud)
                && aud != expectedClientId)
                return Unauthorized(new AuthResponse { Success = false, Message = "Token audience mismatch." });

            // Existing user → login
            var existingUser = await _userRepository.GetUserByEmailAsync(email);
            if (existingUser != null)
            {
                existingUser.IsLoggedIn = true;
                existingUser.LastLoginDate = DateTime.UtcNow;
                await _userRepository.UpdateUserAsync(existingUser);

                var loginToken = _authService.GenerateJwtToken(existingUser);
                var loginDTO = await _userRepository.GetUserProfileById(existingUser.Id)
                               ?? new UserDTO { Id = existingUser.Id, Username = existingUser.Username };

                return Ok(new AuthResponse { Success = true, Message = "Login successful", Token = loginToken, User = loginDTO });
            }

            // New user → register
            tokenInfo.TryGetValue("given_name", out var googleFirst);
            tokenInfo.TryGetValue("family_name", out var googleLast);
            tokenInfo.TryGetValue("name", out var googleFull);

            var firstName = request.FirstNameEn ?? googleFirst ?? googleFull ?? "";
            var lastName  = request.LastNameEn  ?? googleLast  ?? "";

            // Auto-generate a unique username if none supplied
            var username = request.Username?.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                var raw = email.Split('@')[0];
                var cleaned = System.Text.RegularExpressions.Regex.Replace(raw, @"[^a-zA-Z0-9_]", "");
                if (!System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^[a-zA-Z]"))
                    cleaned = "user" + cleaned;
                if (cleaned.Length < 3) cleaned = cleaned.PadRight(3, '0');
                if (cleaned.Length > 18) cleaned = cleaned[..18];

                username = cleaned;
                var suffix = 1;
                while (await _userRepository.UserExistsByUsernameAsync(username))
                    username = cleaned + suffix++;
            }

            var newUser = new User
            {
                Username    = username,
                Email       = email,
                FirstNameEn = firstName,
                LastNameEn  = lastName,
                FirstNameAr = "",
                LastNameAr  = "",
                Dob         = request.Dob?.ToUniversalTime(),
                PhoneNumber = request.PhoneNumber ?? "",
                Password    = _authService.HashPassword(Guid.NewGuid().ToString()),
                CreatedAt   = DateTime.UtcNow,
                IsLoggedIn  = true,
                IsDeleted   = false,
            };

            var created = await _userRepository.CreateUserAsync(newUser);
            var token   = _authService.GenerateJwtToken(created);
            await _userRepository.UpdateUserAsync(created);

            var userDTO = await _userRepository.GetUserProfileById(created.Id)
                          ?? new UserDTO { Id = created.Id, Username = created.Username };

            return Ok(new AuthResponse { Success = true, Message = "Registration successful", Token = token, User = userDTO });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google authentication failed");
            return StatusCode(500, new AuthResponse { Success = false, Message = $"Google authentication failed: {ex.Message}" });
        }
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

            // Username requirements: 3–20 chars, letters/digits/underscores only, must start with a letter
            if (request.Username.Length < 3 || request.Username.Length > 20)
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Username must be between 3 and 20 characters" 
                });

            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Username, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Username must start with a letter and contain only letters, digits, or underscores" 
                });

            // Password requirements: min 8 chars, at least one uppercase, one lowercase, one digit, one special char
            if (request.Password.Length < 8)
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Password must be at least 8 characters long" 
                });

            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Password, @"[A-Z]"))
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Password must contain at least one uppercase letter" 
                });

            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Password, @"[a-z]"))
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Password must contain at least one lowercase letter" 
                });

            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Password, @"[0-9]"))
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Password must contain at least one digit" 
                });

            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Password, @"[^a-zA-Z0-9]"))
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Password must contain at least one special character" 
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

            // If 2FA is enabled, return a pending challenge instead of a token
            if (user.IsTwoFaEnabled)
            {
                var otp = Random.Shared.Next(100_000, 999_999).ToString();
                _cache.Set(OtpCacheKey(user.Id), otp, TimeSpan.FromMinutes(10));

                var email = user.ContactInfo?.PrimaryEmail ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    try { await _emailService.SendTwoFaCodeAsync(email, user.Username, otp); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to send 2FA email to user {UserId}", user.Id); }
                }

                if (_environment.IsDevelopment())
                    _logger.LogInformation("2FA OTP for user {UserId}: {Otp}", user.Id, otp);

                return Ok(new AuthResponse
                {
                    Success = true,
                    RequiresTwoFactor = true,
                    PendingUserId = user.Id,
                    Message = "Verification code sent to your email."
                });
            }

            // Generate JWT token
            var token = _authService.GenerateJwtToken(user);

            user.IsLoggedIn = true;
            user.LastLoginDate = DateTime.UtcNow;
            await _userRepository.UpdateUserAsync(user);

            var userDTO = await _userRepository.GetUserProfileById(user.Id);

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

    [HttpPut("phone-number")]
    public async Task<IActionResult> UpdatePhoneNumber([FromBody] UpdatePhoneNumberRequest request)
    {
        if (request.UserId <= 0)
            return BadRequest(new { success = false, message = "Valid user id is required." });

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { success = false, message = "Phone number is required." });

        var user = await _userRepository.GetUserByIdAsync(request.UserId);
        if (user == null)
            return NotFound(new { success = false, message = "User not found." });

        await _userRepository.UpdatePhoneNumberAsync(request.UserId, request.PhoneNumber.Trim());
        return Ok(new { success = true, message = "Phone number updated successfully." });
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
            
            // Determine frontend URL: prefer Origin header (for tunnel URLs), fall back to config
            string frontendBaseUrl;
            if (Request.Headers.TryGetValue("Origin", out var origin) && !string.IsNullOrEmpty(origin))
            {
                frontendBaseUrl = origin.ToString();
            }
            else if (Request.Headers.TryGetValue("Referer", out var referer) && !string.IsNullOrEmpty(referer))
            {
                // Extract base URL from Referer (e.g., "https://example.com/path" -> "https://example.com")
                var refererUri = new Uri(referer.ToString());
                frontendBaseUrl = $"{refererUri.Scheme}://{refererUri.Host}{(refererUri.IsDefaultPort ? "" : $":{refererUri.Port}")}";
            }
            else
            {
                frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
            }
            
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

    // ── 2FA endpoints ─────────────────────────────────────────────────────────

    [HttpGet("2fa/status")]
    public async Task<IActionResult> TwoFaStatus([FromQuery] int userId)
    {
        if (userId <= 0) return BadRequest(new { success = false, message = "Valid user id is required." });
        var enabled = await _userRepository.IsTwoFaEnabledAsync(userId);
        return Ok(new { success = true, isTwoFaEnabled = enabled });
    }

    [HttpPost("2fa/send-code")]
    public async Task<IActionResult> TwoFaSendCode([FromBody] TwoFaSendCodeRequest request)
    {
        if (request.UserId <= 0) return BadRequest(new { success = false, message = "Valid user id is required." });

        var user = await _userRepository.GetUserByIdAsync(request.UserId);
        if (user == null) return NotFound(new { success = false, message = "User not found." });

        var otp = Random.Shared.Next(100_000, 999_999).ToString();
        _cache.Set(OtpCacheKey(user.Id), otp, TimeSpan.FromMinutes(10));

        var email = user.ContactInfo?.PrimaryEmail ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { success = false, message = "No email address on file." });

        try
        {
            await _emailService.SendTwoFaCodeAsync(email, user.Username, otp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send 2FA code to user {UserId}", user.Id);
            if (_environment.IsDevelopment())
                return Ok(new { success = true, message = $"[Dev] Code: {otp}" });
            return StatusCode(500, new { success = false, message = "Failed to send verification code." });
        }

        if (_environment.IsDevelopment())
            _logger.LogInformation("2FA OTP for user {UserId}: {Otp}", user.Id, otp);

        return Ok(new { success = true, message = "Verification code sent to your email." });
    }

    [HttpPost("2fa/enable")]
    public async Task<IActionResult> TwoFaEnable([FromBody] TwoFaVerifyRequest request)
    {
        if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { success = false, message = "User id and code are required." });

        if (!_cache.TryGetValue(OtpCacheKey(request.UserId), out string? stored) || stored != request.Code.Trim())
            return BadRequest(new { success = false, message = "Invalid or expired code." });

        _cache.Remove(OtpCacheKey(request.UserId));
        await _userRepository.SetTwoFaEnabledAsync(request.UserId, true);
        return Ok(new { success = true, message = "Two-factor authentication enabled." });
    }

    [HttpPost("2fa/disable")]
    public async Task<IActionResult> TwoFaDisable([FromBody] TwoFaSendCodeRequest request)
    {
        if (request.UserId <= 0) return BadRequest(new { success = false, message = "Valid user id is required." });
        await _userRepository.SetTwoFaEnabledAsync(request.UserId, false);
        _cache.Remove(OtpCacheKey(request.UserId));
        return Ok(new { success = true, message = "Two-factor authentication disabled." });
    }

    [HttpPost("2fa/login")]
    public async Task<IActionResult> TwoFaLogin([FromBody] TwoFaVerifyRequest request)
    {
        if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { success = false, message = "User id and code are required." });

        if (!_cache.TryGetValue(OtpCacheKey(request.UserId), out string? stored) || stored != request.Code.Trim())
            return BadRequest(new { success = false, message = "Invalid or expired code." });

        _cache.Remove(OtpCacheKey(request.UserId));

        var user = await _userRepository.GetUserByIdAsync(request.UserId);
        if (user == null) return NotFound(new { success = false, message = "User not found." });

        user.IsLoggedIn = true;
        user.LastLoginDate = DateTime.UtcNow;
        await _userRepository.UpdateUserAsync(user);

        var token = _authService.GenerateJwtToken(user);
        var userDTO = await _userRepository.GetUserProfileById(user.Id) ?? new UserDTO { Id = user.Id, Username = user.Username };

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            Token = token,
            User = userDTO
        });
    }
}
