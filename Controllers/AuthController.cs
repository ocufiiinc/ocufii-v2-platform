
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OcufiiAPI.Configs;
using OcufiiAPI.DTO;
using OcufiiAPI.Models;
using OcufiiAPI.Repositories;
using OcufiiAPI.Validators;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;

namespace OcufiiAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<Role> _roleRepo;
    private readonly IRepository<RefreshToken> _refreshRepo;
    private readonly JwtConfig _jwt;
    private readonly LegacyConfig _legacy;
    private readonly PasswordHasher<User> _hasher = new();
    private readonly OcufiiDbContext _db;

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
        _db = db;
        _refreshRepo = refreshRepo;
        _jwt = jwtOptions.Value;
        _legacy = legacyOptions.Value;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userRepo.Query()
            .Where(u => u.Email == dto.Email && !u.IsDeleted)
            .Include(u => u.Role)
            .FirstOrDefaultAsync();

        if (user == null || _hasher.VerifyHashedPassword(user, user.Password, dto.Password)
            == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new ApiResponse(false, "Invalid email or password"));
        }

        var (accessToken, refreshToken) = GenerateTokens(user);
        await SaveRefreshToken(user.UserId, refreshToken);

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
                user = new { user.Email, user.FirstName, user.LastName, user.Company },
                deviceToken = userDeviceToken
            }
        });
    }

    [HttpPost("device-token")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> RegisterDeviceToken([FromBody] DeviceTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceTokenValue))
            return BadRequest(new ApiResponse(false, "DeviceTokenValue is required"));

        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var tokenByValue = await _db.DeviceToken
            .FirstOrDefaultAsync(t => t.DeviceTokenValue == request.DeviceTokenValue);

        var existingForUser = await _db.DeviceToken
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (tokenByValue != null)
        {
            if (tokenByValue.UserId == userId)
            {

                tokenByValue.MobileDevice = request.MobileDevice;
                tokenByValue.MobileOsVersion = request.MobileOsVersion;
                tokenByValue.Version = request.Version;

                _db.DeviceToken.Update(tokenByValue);
                await _db.SaveChangesAsync();

                return Ok(new ApiResponse(true, "Device token updated for current user"));
            }
            else
            {
                tokenByValue.UserId = userId;
                tokenByValue.MobileDevice = request.MobileDevice;
                tokenByValue.MobileOsVersion = request.MobileOsVersion;
                tokenByValue.Version = request.Version;

                _db.DeviceToken.Update(tokenByValue);
                await _db.SaveChangesAsync();

                return Ok(new ApiResponse(true, "Device token reassigned to current user"));
            }
        }

        if (existingForUser != null)
        {
            existingForUser.DeviceTokenValue = request.DeviceTokenValue;
            existingForUser.MobileDevice = request.MobileDevice;
            existingForUser.MobileOsVersion = request.MobileOsVersion;
            existingForUser.Version = request.Version;

            _db.DeviceToken.Update(existingForUser);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse(true, "User's existing token updated with new value"));
        }

        var newToken = new DeviceToken
        {
            UserId = userId,
            DeviceTokenValue = request.DeviceTokenValue,
            MobileDevice = request.MobileDevice,
            MobileOsVersion = request.MobileOsVersion,
            Version = request.Version
        };

        _db.DeviceToken.Add(newToken);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "New device token registered"));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
    [FromBody] RegisterRequestDto dto,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null)
    {
        var validator = new RegisterRequestValidator();
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            throw new FluentValidation.ValidationException(validationResult.Errors);
        }

        var existingUser = (await _userRepo.FindAsync(u => u.Email == dto.Email)).Any();
        if (existingUser)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://errors.ocufii.com/email-exists",
                Title = "Email already in use",
                Status = 409,
                Detail = "A user with this email address already exists",
                Instance = "/api/auth/register"
            });
        }

        var role = (await _roleRepo.FindAsync(r => r.RoleName == _legacy.RegistrationRole))
                        .FirstOrDefault();

        if (role == null)
            throw new InvalidOperationException($"Role '{_legacy.RegistrationRole}' not found");

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

    [HttpPost("refresh")]
    [Authorize]
    public async Task<IActionResult> Refresh([FromBody] RefreshDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
            return BadRequest(new ApiResponse(false, "Refresh token is required"));

        var tokenRecord = (await _refreshRepo.FindAsync(t => t.Token == dto.RefreshToken && t.IsActive))
                              .FirstOrDefault();

        if (tokenRecord == null || tokenRecord.ExpiresAt <= DateTime.UtcNow)
        {
            return Unauthorized(new ApiResponse(false, "Invalid or expired refresh token"));
        }

        var user = (await _userRepo.FindAsync(u => u.UserId == tokenRecord.UserId && !u.IsDeleted))
                        .FirstOrDefault();

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

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            var token = (await _refreshRepo.FindAsync(t => t.Token == dto.RefreshToken))
                             .FirstOrDefault();
            if (token != null)
            {
                token.Revoke();
                _refreshRepo.Update(token);
                await _refreshRepo.SaveAsync();
            }
        }

        return Ok(new ApiResponse(true, "Logged out successfully"));
    }

    [HttpGet("/validate-email/{email}")]
    public async Task<IActionResult> ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            return BadRequest(new ApiResponse(false, "Invalid email format"));

        var exists = (await _userRepo.FindAsync(u => u.Email == email)).Any();

        return Ok(new ApiResponse(true, exists ? "Email already exists" : "Email is available")
        {
            Data = new { available = !exists }
        });
    }

    [HttpPut("change-password")]
    [Authorize]
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

    [HttpPut("change-email")]
    [Authorize]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiResponse(false, "Invalid request"));

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new ApiResponse(false, "Invalid token"));

        var user = (await _userRepo.FindAsync(u => u.UserId == userId && !u.IsDeleted))
                        .FirstOrDefault();

        if (user == null)
            return NotFound(new ApiResponse(false, "User not found"));

        var emailExists = (await _userRepo.FindAsync(u => u.Email == dto.NewEmail && u.UserId != userId)).Any();
        if (emailExists)
            return Conflict(new ApiResponse(false, "Email already in use"));

        user.Email = dto.NewEmail;
        user.DateUpdated = DateTime.UtcNow;
        _userRepo.Update(user);
        await _userRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Email changed successfully"));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = (await _userRepo.FindAsync(u => u.UserId == userId))
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
                    .FirstOrDefault();

        return Ok(new ApiResponse(true, "Profile retrieved")
        {
            Data = user
        });
    }

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