using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OcufiiAPI.Configs;
using OcufiiAPI.Data;
using OcufiiAPI.DTO;
using OcufiiAPI.Models;
using OcufiiAPI.Repositories;
using OcufiiAPI.Validators;
using Serilog;
using Swashbuckle.AspNetCore.Annotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OcufiiAPI.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse), 200)]
[ProducesResponseType(typeof(ProblemDetails), 400)]
[ProducesResponseType(typeof(ProblemDetails), 401)]
[ProducesResponseType(typeof(ProblemDetails), 403)]
[ProducesResponseType(typeof(ProblemDetails), 409)]
[ProducesResponseType(typeof(ProblemDetails), 500)]
public class AuthController : ControllerBase
{
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<Role> _roleRepo;
    private readonly IRepository<RefreshToken> _refreshRepo;
    private readonly OcufiiDbContext _db;
    private readonly JwtConfig _jwt;
    private readonly LegacyConfig _legacy;
    private readonly PasswordHasher<User> _hasher = new();

    public AuthController(
        IRepository<User> userRepo,
        IRepository<Role> roleRepo,
        IRepository<RefreshToken> refreshRepo,
        OcufiiDbContext db,
        IOptions<JwtConfig> jwtOptions,
        IOptions<LegacyConfig> legacyOptions)
    {
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _refreshRepo = refreshRepo;
        _db = db;
        _jwt = jwtOptions.Value;
        _legacy = legacyOptions.Value;
    }

    /// <summary>
    /// Authenticate user and return JWT access token + refresh token
    /// </summary>
    /// <remarks>
    /// On success, returns:
    /// - access_token (JWT for authenticated requests)
    /// - refresh_token (for obtaining new access tokens)
    /// - user basic info
    /// - deviceToken (latest stored Firebase token for this user, if any)
    ///
    /// Client should compare the returned deviceTokenValue with current FCM token.
    /// If different, call POST /api/auth/device-token to update.
    /// </remarks>
    /// <param name="dto">Login credentials and optional device token</param>
    /// <response code="200">Login successful</response>
    /// <response code="400">Invalid request body or validation failed</response>
    /// <response code="401">Invalid email or password</response>
    /// <response code="500">Internal server error (no details exposed)</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "User Login",
        Description = "Authenticates user and returns tokens + stored Firebase device token (if any)")]
    [SwaggerResponse(200, "Login successful", typeof(ApiResponse))]
    [SwaggerResponse(400, "Bad request - validation failed")]
    [SwaggerResponse(401, "Unauthorized - invalid credentials")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userRepo.Query()
            .Where(u => u.Email == dto.Email && !u.IsDeleted)
            .Include(u => u.Role)
            .FirstOrDefaultAsync();

        if (user == null || _hasher.VerifyHashedPassword(user, user.Password, dto.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new ApiResponse(false, "Invalid email or password"));

        var (accessToken, refreshToken) = GenerateTokens(user);
        await SaveRefreshToken(user.UserId, refreshToken);

        // Fetch latest device token for this user
        var userDeviceToken = await _db.DeviceToken
            .Where(t => t.UserId == user.UserId)
            .OrderByDescending(t => t.DeviceTokenId)
            .Select(t => new
            {
                deviceTokenValue = t.DeviceTokenValue,
                mobileDevice = t.MobileDevice,
                mobileOsVersion = t.MobileOsVersion,
                version = t.Version
            })
            .FirstOrDefaultAsync();

        Log.Information("User logged in: {Email}", user.Email);

        return Ok(new ApiResponse(true, "Login successful")
        {
            Data = new
            {
                access_token = accessToken,
                refresh_token = refreshToken,
                user = new
                {
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.Company
                },
                deviceToken = userDeviceToken
            }
        });
    }

    /// <summary>
    /// Register or update Firebase device token for authenticated user
    /// </summary>
    /// <remarks>
    /// - If token already exists for another user → reassigns to current user
    /// - If token exists for current user → updates metadata
    /// - If new token → creates new record
    /// </remarks>
    /// <param name="request">Device token details</param>
    /// <response code="200">Token registered or updated</response>
    /// <response code="400">DeviceTokenValue is required</response>
    /// <response code="401">Unauthorized</response>
    [HttpPost("device-token")]
    [SwaggerOperation(
        Summary = "Register/Update Firebase Device Token",
        Description = "Registers or updates the Firebase token for the authenticated user")]
    [SwaggerResponse(200, "Token registered/updated")]
    [SwaggerResponse(400, "Invalid request - DeviceTokenValue required")]
    public async Task<ActionResult<ApiResponse>> RegisterDeviceToken([FromBody] DeviceTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceTokenValue))
            return BadRequest(new ApiResponse(false, "DeviceTokenValue is required"));

        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var existing = await _db.DeviceToken
            .FirstOrDefaultAsync(t => t.DeviceTokenValue == request.DeviceTokenValue);

        if (existing != null)
        {
            existing.UserId = userId;
            existing.MobileDevice = request.MobileDevice;
            existing.MobileOsVersion = request.MobileOsVersion;
            existing.Version = request.Version;

            _db.DeviceToken.Update(existing);
        }
        else
        {
            var newToken = new DeviceToken
            {
                UserId = userId,
                DeviceTokenValue = request.DeviceTokenValue,
                MobileDevice = request.MobileDevice,
                MobileOsVersion = request.MobileOsVersion,
                Version = request.Version
            };
            _db.DeviceToken.Add(newToken);
        }

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Device token registered/updated"));
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    /// <remarks>
    /// Creates a new user account with default role (viewer).
    /// Email must be unique.
    /// </remarks>
    /// <param name="dto">Registration details</param>
    /// <param name="idempotencyKey">Optional idempotency key to prevent duplicate registrations</param>
    /// <response code="201">User registered successfully</response>
    /// <response code="400">Validation failed</response>
    /// <response code="409">Email already in use</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "User Registration", Description = "Creates a new user account")]
    [SwaggerResponse(201, "User registered")]
    [SwaggerResponse(400, "Invalid request")]
    [SwaggerResponse(409, "Email already exists")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequestDto dto,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null)
    {
        var validator = new RegisterRequestValidator();
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiResponse(false, "Validation failed")
            {
                Data = new
                {
                    errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray()
                }
            });
        }

        var existingUser = await _userRepo.Query().AnyAsync(u => u.Email == dto.Email);
        if (existingUser)
            return Conflict(new ApiResponse(false, "Email already in use"));

        var role = await _roleRepo.Query()
            .FirstOrDefaultAsync(r => r.RoleName == _legacy.RegistrationRole);

        if (role == null)
            return StatusCode(500, new ApiResponse(false, "Default role not found"));

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PhoneNumber = dto.PhoneNumber,
            Company = dto.Company ?? "Ocufii User",
            Username = dto.UserName ?? dto.Email.Split('@')[0],
            Password = _hasher.HashPassword(null!, dto.Password),
            RoleId = role.RoleId,
            TenantId = Guid.Parse(_legacy.FixedTenantId),
            AccountHold = dto.AccountHold,
            DateSubmitted = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            SubscriptionDate = dto.SubscriptionDate ?? DateTime.UtcNow,
            GmtInfo = dto.GtmInfo ?? "",
            AccountType = "single",
            IsEnabled = true,
            IsDeleted = false
        };

        await _userRepo.AddAsync(user);
        await _userRepo.SaveAsync();

        Log.Information("User registered successfully: {Email} | Idempotency-Key: {Key}", user.Email, idempotencyKey);

        return Created($"/api/users/{user.UserId}", new ApiResponse(true, "Registration successful")
        {
            Data = new
            {
                user.UserId,
                user.Email,
                user.FirstName,
                user.LastName
            }
        });
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    /// <remarks>
    /// Revokes old refresh token and issues new access + refresh tokens.
    /// </remarks>
    /// <param name="dto">Refresh token</param>
    /// <response code="200">Tokens refreshed</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Invalid or expired refresh token</response>
    [HttpPost("refresh")]
    [Authorize]
    [SwaggerOperation(Summary = "Refresh Access Token")]
    [SwaggerResponse(200, "Tokens refreshed")]
    [SwaggerResponse(400, "Invalid request")]
    [SwaggerResponse(401, "Invalid or expired refresh token")]
    public async Task<IActionResult> Refresh([FromBody] RefreshDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
            return BadRequest(new ApiResponse(false, "Refresh token is required"));

        var tokenRecord = await _refreshRepo.Query()
            .FirstOrDefaultAsync(t => t.Token == dto.RefreshToken && t.IsActive);

        if (tokenRecord == null || tokenRecord.ExpiresAt <= DateTime.UtcNow)
            return Unauthorized(new ApiResponse(false, "Invalid or expired refresh token"));

        var user = await _userRepo.Query()
            .FirstOrDefaultAsync(u => u.UserId == tokenRecord.UserId && !u.IsDeleted);

        if (user == null)
            return Unauthorized(new ApiResponse(false, "User not found"));

        tokenRecord.Revoke();
        _refreshRepo.Update(tokenRecord);

        var (newAccessToken, newRefreshToken) = GenerateTokens(user);
        await SaveRefreshToken(user.UserId, newRefreshToken);

        await _refreshRepo.SaveAsync();

        Log.Information("Token refreshed for user: {Email}", user.Email);

        return Ok(new ApiResponse(true, "Token refreshed successfully")
        {
            Data = new { access_token = newAccessToken, refresh_token = newRefreshToken }
        });
    }

    /// <summary>
    /// Logout user by revoking refresh token
    /// </summary>
    /// <param name="dto">Refresh token to revoke</param>
    /// <response code="200">Logged out</response>
    [HttpPost("logout")]
    [Authorize]
    [SwaggerOperation(Summary = "User Logout")]
    [SwaggerResponse(200, "Logged out successfully")]
    public async Task<IActionResult> Logout([FromBody] RefreshDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            var token = await _refreshRepo.Query()
                .FirstOrDefaultAsync(t => t.Token == dto.RefreshToken);

            if (token != null)
            {
                token.Revoke();
                _refreshRepo.Update(token);
                await _refreshRepo.SaveAsync();
            }
        }

        return Ok(new ApiResponse(true, "Logged out successfully"));
    }

    /// <summary>
    /// Get current user's profile
    /// </summary>
    /// <response code="200">Profile retrieved</response>
    /// <response code="401">Unauthorized</response>
    [HttpGet("me")]
    [Authorize]
    [SwaggerOperation(Summary = "Get Current User Profile")]
    [SwaggerResponse(200, "Profile retrieved")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _userRepo.Query()
            .Where(u => u.UserId == userId && !u.IsDeleted)
            .Select(u => new
            {
                u.UserId,
                u.Email,
                u.FirstName,
                u.LastName,
                u.PhoneNumber,
                u.Company,
                u.AccountType,
                u.IsEnabled
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(new ApiResponse(false, "User not found"));

        return Ok(new ApiResponse(true, "Profile retrieved")
        {
            Data = user
        });
    }

    /// <summary>
    /// Change current user's password
    /// </summary>
    /// <response code="200">Password changed</response>
    /// <response code="400">Invalid current password or validation failed</response>
    [HttpPut("change-password")]
    [Authorize]
    [SwaggerOperation(Summary = "Change Password")]
    [SwaggerResponse(200, "Password changed")]
    [SwaggerResponse(400, "Invalid current password or validation failed")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _userRepo.GetByIdAsync(userId);

        var verify = _hasher.VerifyHashedPassword(user!, user!.Password, dto.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
            return BadRequest(new ApiResponse(false, "Current password is incorrect"));

        user.Password = _hasher.HashPassword(user, dto.NewPassword);
        user.DateUpdated = DateTime.UtcNow;
        _userRepo.Update(user);
        await _userRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Password changed successfully"));
    }

    /// <summary>
    /// Change current user's email
    /// </summary>
    /// <response code="200">Email changed</response>
    /// <response code="400">Invalid request</response>
    /// <response code="409">Email already in use</response>
    [HttpPut("change-email")]
    [Authorize]
    [SwaggerOperation(Summary = "Change Email")]
    [SwaggerResponse(200, "Email changed")]
    [SwaggerResponse(400, "Invalid request")]
    [SwaggerResponse(409, "Email already in use")]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailDto dto)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _userRepo.Query()
            .FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted);

        if (user == null)
            return NotFound(new ApiResponse(false, "User not found"));

        var emailExists = await _userRepo.Query()
            .AnyAsync(u => u.Email == dto.NewEmail && u.UserId != userId);

        if (emailExists)
            return Conflict(new ApiResponse(false, "Email already in use"));

        user.Email = dto.NewEmail;
        user.DateUpdated = DateTime.UtcNow;
        _userRepo.Update(user);
        await _userRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Email changed successfully"));
    }

    // Private Helpers
    private (string accessToken, string refreshToken) GenerateTokens(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new Claim("tenant_id", _legacy.FixedTenantId),
            new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "viewer")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var accessToken = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
            signingCredentials: creds);

        var refreshToken = Guid.NewGuid().ToString("N");

        return (new JwtSecurityTokenHandler().WriteToken(accessToken), refreshToken);
    }

    private async Task SaveRefreshToken(Guid userId, string token)
    {
        var rt = new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshRepo.AddAsync(rt);
        await _refreshRepo.SaveAsync();
    }
}

// DTOs
public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DeviceTokenValue { get; set; }
    public string? MobileDevice { get; set; }
    public string? MobileOsVersion { get; set; }
    public string? Version { get; set; }
}

public class DeviceTokenRequest
{
    public string DeviceTokenValue { get; set; } = string.Empty;
    public string? MobileDevice { get; set; }
    public string? MobileOsVersion { get; set; }
    public string? Version { get; set; }
}