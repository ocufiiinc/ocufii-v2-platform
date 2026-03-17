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
using OcufiiAPI.Enums;
using OcufiiAPI.Models;
using OcufiiAPI.Repositories;
using OcufiiAPI.Services;
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
public class AuthController : ControllerBase
{
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<Role> _roleRepo;
    private readonly IRepository<RefreshToken> _refreshRepo;
    private readonly IEmailService _emailService;
    private readonly OcufiiDbContext _db;
    private readonly JwtConfig _jwt;
    private readonly LegacyConfig _legacy;
    private readonly PasswordHasher<User> _hasher = new();
    private readonly PasswordHasher<PlatformAdmin> _platformHasher = new();
    private readonly PasswordHasher<Reseller> _resellerHasher = new();

    public AuthController(
        IRepository<User> userRepo,
        IRepository<Role> roleRepo,
        IRepository<RefreshToken> refreshRepo,
        IEmailService emailService,
        OcufiiDbContext db,
        IOptions<JwtConfig> jwtOptions,
        IOptions<LegacyConfig> legacyOptions)
    {
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _refreshRepo = refreshRepo;
        _emailService = emailService;
        _db = db;
        _jwt = jwtOptions.Value;
        _legacy = legacyOptions.Value;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "User Login", Description = "Authenticates user and returns tokens")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        // Only active users (not soft-deleted)
        var user = await _userRepo.Query()
            .Where(u => u.Email == dto.Email && !u.IsDeleted && u.DeletedAt == null)
            .Include(u => u.Role)
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync();

        if (user == null || _hasher.VerifyHashedPassword(user, user.Password, dto.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new ApiResponse(false, "Invalid email or password") { ErrorCode = "OC-014" });

        if (user.Tenant != null && !user.Tenant.IsActive)
            return Unauthorized(new ApiResponse(false, "Your tenant account has been deactivated. Contact your reseller.") { ErrorCode = "OC-091" });

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
                user = new
                {
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.Company
                },
                deviceToken = userDeviceToken
            },
            ErrorCode = null
        });
    }

    [HttpPost("device-token")]
    public async Task<ActionResult<ApiResponse>> RegisterDeviceToken([FromBody] DeviceTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceTokenValue))
            return BadRequest(new ApiResponse(false, "DeviceTokenValue is required") { ErrorCode = "OC-015" });

        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var existingToken = await _db.DeviceToken
            .FirstOrDefaultAsync(t => t.DeviceTokenValue == request.DeviceTokenValue);

        if (existingToken != null)
        {
            if (existingToken.UserId != userId)
            {
                _db.DeviceToken.Remove(existingToken);
            }
        }

        var userToken = await _db.DeviceToken
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (userToken != null)
        {
            userToken.DeviceTokenValue = request.DeviceTokenValue;
            userToken.MobileDevice = request.MobileDevice;
            userToken.MobileOsVersion = request.MobileOsVersion;
            userToken.Version = request.Version;
            _db.DeviceToken.Update(userToken);
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

        return Ok(new ApiResponse(true, "Device token registered/updated")
        {
            ErrorCode = null
        });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "User Registration", Description = "Creates a new tenant user account with default permissions & features from reseller")]
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
                Data = new { errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray() },
                ErrorCode = "OC-003"
            });
        }

        var existingUser = await _db.Users
            .AnyAsync(u => u.Email == dto.Email && !u.IsDeleted && u.DeletedAt == null);

        if (existingUser)
            return Conflict(new ApiResponse(false, "Email already in use") { ErrorCode = "OC-004" });

        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.RoleName == "account_owner");

        if (role == null)
            return StatusCode(500, new ApiResponse(false, "Default role not found") { ErrorCode = "OC-005" });

        var assignedResellerId = new Guid("00000000-0000-0000-0000-000000000001");

        var newTenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            AssignedResellerId = assignedResellerId,
            DateCreated = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            ThemeConfig = "{}",
            CustomWorkflows = "[]",
            IsActive = true
        };

        _db.Tenants.Add(newTenant);
        await _db.SaveChangesAsync();

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
            TenantId = newTenant.TenantId,
            AccountHold = dto.AccountHold,
            DateSubmitted = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            SubscriptionDate = dto.SubscriptionDate ?? DateTime.UtcNow,
            GmtInfo = dto.GtmInfo ?? "",
            AccountType = "single",
            IsEnabled = true,
            IsDeleted = false,
            DeletedAt = null
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        try
        {

            var allowedTenantPermIds = await _db.ResellerPermissions
                .Where(rp => rp.ResellerId == assignedResellerId && rp.IsGranted)
                .Join(_db.Permissions,
                      rp => rp.PermissionId,
                      p => p.PermissionId,
                      (rp, p) => new { p.PermissionId, p.Category })
                .Where(x => x.Category == "account")
                .Select(x => x.PermissionId)
                .ToListAsync();

            foreach (var permId in allowedTenantPermIds)
            {
                _db.TenantPermissions.Add(new TenantPermission
                {
                    TenantId = newTenant.TenantId,
                    PermissionId = permId,
                    IsGranted = true,  // default granted
                    GrantedByResellerId = assignedResellerId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Get features the assigned reseller has enabled
            var allowedFeatureIds = await _db.ResellerFeatures
                .Where(rf => rf.ResellerId == assignedResellerId && rf.IsEnabled)
                .Select(rf => rf.FeatureId)
                .ToListAsync();

            // Assign default tenant features
            foreach (var featureId in allowedFeatureIds)
            {
                _db.TenantFeatures.Add(new TenantFeature
                {
                    TenantId = newTenant.TenantId,
                    FeatureId = featureId,
                    IsEnabled = true,
                    GrantedByResellerId = assignedResellerId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Free plan subscription (unchanged)
            var freePlan = new SubscriptionPlan
            {
                UserId = user.UserId,
                PlanType = SubscriptionPlanType.Free,
                MaxActiveLinks = 1,
                IsActive = true,
                ExpiryDate = DateTime.UtcNow.AddYears(10),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.SubscriptionPlans.Add(freePlan);
            await _db.SaveChangesAsync();

            Log.Information("User registered successfully: {Email} | Idempotency-Key: {Key}", user.Email, idempotencyKey);

            return Created($"/api/users/{user.UserId}", new ApiResponse(true, "Registration successful")
            {
                Data = new
                {
                    user.UserId,
                    user.Email,
                    user.FirstName,
                    user.LastName
                },
                ErrorCode = null
            });
        }
        catch (Exception ex)
        {
            _db.Tenants.Remove(newTenant);
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            Log.Error(ex, "Registration failed for {Email}", dto.Email);
            return StatusCode(500, new ApiResponse(false, "Registration failed. Please try again.") { ErrorCode = "OC-999" });
        }
    }

    [HttpPost("refresh")]
    [Authorize]
    [SwaggerOperation(Summary = "Refresh Access Token")]
    public async Task<IActionResult> Refresh([FromBody] RefreshDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
            return BadRequest(new ApiResponse(false, "Refresh token is required")
            {
                ErrorCode = "OC-006"
            });

        var tokenRecord = await _refreshRepo.Query()
            .FirstOrDefaultAsync(t => t.Token == dto.RefreshToken && t.IsActive);

        if (tokenRecord == null || tokenRecord.ExpiresAt <= DateTime.UtcNow)
            return Unauthorized(new ApiResponse(false, "Invalid or expired refresh token")
            {
                ErrorCode = "OC-007"
            });

        var user = await _userRepo.Query()
            .FirstOrDefaultAsync(u => u.UserId == tokenRecord.UserId && !u.IsDeleted);

        if (user == null)
            return Unauthorized(new ApiResponse(false, "User not found")
            {
                ErrorCode = "OC-008"
            });

        tokenRecord.Revoke();
        _refreshRepo.Update(tokenRecord);

        var (newAccessToken, newRefreshToken) = GenerateTokens(user);
        await SaveRefreshToken(user.UserId, newRefreshToken);
        await _refreshRepo.SaveAsync();

        Log.Information("Token refreshed for user: {Email}", user.Email);

        return Ok(new ApiResponse(true, "Token refreshed successfully")
        {
            Data = new { access_token = newAccessToken, refresh_token = newRefreshToken },
            ErrorCode = null
        });
    }

    [HttpPost("logout")]
    [Authorize]
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

        return Ok(new ApiResponse(true, "Logged out successfully")
        {
            ErrorCode = null
        });
    }

    [HttpGet("me")]
    [Authorize]
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
            return NotFound(new ApiResponse(false, "User not found")
            {
                ErrorCode = "OC-009"
            });

        return Ok(new ApiResponse(true, "Profile retrieved")
        {
            Data = user,
            ErrorCode = null
        });
    }

    [HttpPut("change-password")]
    [Authorize]
    [SwaggerOperation(Summary = "Change Password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _userRepo.GetByIdAsync(userId);
        var verify = _hasher.VerifyHashedPassword(user!, user!.Password, dto.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
            return BadRequest(new ApiResponse(false, "Current password is incorrect")
            {
                ErrorCode = "OC-010"
            });

        user.Password = _hasher.HashPassword(user, dto.NewPassword);
        user.DateUpdated = DateTime.UtcNow;
        _userRepo.Update(user);
        await _userRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Password changed successfully")
        {
            ErrorCode = null
        });
    }

    [HttpPut("change-email")]
    [Authorize]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailDto dto)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _userRepo.Query()
            .FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted);

        if (user == null)
            return NotFound(new ApiResponse(false, "User not found")
            {
                ErrorCode = "OC-009"
            });

        var emailExists = await _userRepo.Query()
            .AnyAsync(u => u.Email == dto.NewEmail && u.UserId != userId);

        if (emailExists)
            return Conflict(new ApiResponse(false, "Email already in use")
            {
                ErrorCode = "OC-004"
            });

        user.Email = dto.NewEmail;
        user.DateUpdated = DateTime.UtcNow;
        _userRepo.Update(user);
        await _userRepo.SaveAsync();

        return Ok(new ApiResponse(true, "Email changed successfully")
        {
            ErrorCode = null
        });
    }

    [HttpGet("email/{email}/validate")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new ApiResponse(false, "Email is required") { ErrorCode = "OC-080" });

        email = email.Trim().ToLowerInvariant();

        var userExists = await _db.Users
            .AnyAsync(u => u.Email == email && !u.IsDeleted && u.DeletedAt == null);

        if (userExists)
        {
            return Ok(new ApiResponse(true, "Email is already in use")
            {
                Data = new { email, isAvailable = false, message = "Email is already in use." },
                ErrorCode = "OC-081"
            });
        }

        return Ok(new ApiResponse(true, "Email is available")
        {
            Data = new { email, isAvailable = true, message = "Email is available for registration." },
            ErrorCode = null
        });
    }

    [HttpPost("platform-login")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Platform & Reseller Login")]
    public async Task<IActionResult> PlatformLogin([FromBody] PlatformLoginDto dto)
    {
        var platformAdmin = await _db.PlatformAdmins
            .FirstOrDefaultAsync(a => a.Email == dto.Email);

        if (platformAdmin != null)
        {
            if (!platformAdmin.IsActive)
                return Unauthorized(new ApiResponse(false, "Account deactivated. Contact Ocufii support.") { ErrorCode = "OC-064" });

            var verificationResult = _platformHasher.VerifyHashedPassword(
                platformAdmin,
                platformAdmin.PasswordHash,
                dto.Password
            );

            if (verificationResult == PasswordVerificationResult.Failed)
                return Unauthorized(new ApiResponse(false, "Invalid email or password") { ErrorCode = "OC-011" });

            var claims = new[]
            {
            new Claim(ClaimTypes.Email, platformAdmin.Email),
            new Claim(ClaimTypes.NameIdentifier, platformAdmin.AdminId.ToString()),
            new Claim(ClaimTypes.Role, platformAdmin.Role ?? "super_admin"),
            new Claim("user_type", "platform")
        };

            var accessToken = GenerateAccessToken(claims);

            platformAdmin.LastLogin = DateTime.UtcNow;
            _db.PlatformAdmins.Update(platformAdmin);
            await _db.SaveChangesAsync();

            // Fetch granted permissions (separate query - safe for EF)
            var permissions = await _db.PlatformPermissions
                .Where(pp => pp.AdminId == platformAdmin.AdminId && pp.IsGranted)
                .Join(_db.Permissions,
                      pp => pp.PermissionId,
                      p => p.PermissionId,
                      (pp, p) => new
                      {
                          PermissionId = pp.PermissionId,
                          Key = p.Key,
                          IsGranted = pp.IsGranted
                      })
                .ToListAsync();

            return Ok(new ApiResponse(true, "Platform login successful")
            {
                Data = new
                {
                    access_token = accessToken,
                    role = platformAdmin.Role ?? "super_admin",
                    user_type = "platform",
                    permissions  // array of { permissionId, key, isGranted }
                },
                ErrorCode = null
            });
        }

        // RESELLER LOGIN
        var reseller = await _db.Resellers
            .FirstOrDefaultAsync(r => r.Email == dto.Email);

        if (reseller != null)
        {
            if (!reseller.IsActive)
                return Unauthorized(new ApiResponse(false, "Reseller account is deactivated. Contact Ocufii support.") { ErrorCode = "OC-064" });

            var verificationResult = _resellerHasher.VerifyHashedPassword(
                reseller,
                reseller.PasswordHash,
                dto.Password
            );

            if (verificationResult == PasswordVerificationResult.Failed)
                return Unauthorized(new ApiResponse(false, "Invalid email or password") { ErrorCode = "OC-011" });

            var claims = new[]
            {
            new Claim(ClaimTypes.Email, reseller.Email),
            new Claim(ClaimTypes.NameIdentifier, reseller.ResellerId.ToString()),
            new Claim(ClaimTypes.Role, "reseller_admin"),
            new Claim("user_type", "reseller"),
            new Claim("reseller_id", reseller.ResellerId.ToString())
        };

            var accessToken = GenerateAccessToken(claims);

            // Fetch granted permissions (separate query)
            var permissions = await _db.ResellerPermissions
                .Where(rp => rp.ResellerId == reseller.ResellerId && rp.IsGranted)
                .Join(_db.Permissions,
                      rp => rp.PermissionId,
                      p => p.PermissionId,
                      (rp, p) => new
                      {
                          PermissionId = rp.PermissionId,
                          Key = p.Key,
                          IsGranted = rp.IsGranted
                      })
                .ToListAsync();

            return Ok(new ApiResponse(true, "Reseller login successful")
            {
                Data = new
                {
                    access_token = accessToken,
                    role = "reseller_admin",
                    user_type = "reseller",
                    permissions  // array of { permissionId, key, isGranted }
                },
                ErrorCode = null
            });
        }

        return Unauthorized(new ApiResponse(false, "Invalid email or password") { ErrorCode = "OC-011" });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var validator = new ForgotPasswordValidator();
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiResponse(false, "Validation failed")
            {
                Data = new { errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray() },
                ErrorCode = "OC-003"
            });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant() && !u.IsDeleted && u.DeletedAt == null);

        // Always return 200 OK (prevents email enumeration attack)
        if (user == null)
        {
            Log.Information("Forgot password requested for non-existent email: {Email}", dto.Email);
            return Ok(new ApiResponse(true, "If this email is registered, a reset OTP has been sent.") { ErrorCode = null });
        }

        // Generate 6-digit OTP
        var otp = new Random().Next(100000, 999999).ToString();
        var otpExpiry = DateTime.UtcNow.AddMinutes(120);

        // Store OTP in user record
        user.OTP = otp;
        user.OTPExpiry = otpExpiry;
        user.DateUpdated = DateTime.UtcNow;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();

        // Send OTP email
        await _emailService.SendPasswordResetOtpAsync(dto.Email, otp, 120);

        Log.Information("Password reset OTP sent to {Email}", dto.Email);

        return Ok(new ApiResponse(true, "If this email is registered, a reset OTP has been sent.") { ErrorCode = null });
    }

    [HttpPut("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var validator = new ResetPasswordValidator();
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiResponse(false, "Validation failed")
            {
                Data = new { errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray() },
                ErrorCode = "OC-003"
            });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant() && !u.IsDeleted && u.DeletedAt == null);

        if (user == null)
            return BadRequest(new ApiResponse(false, "Invalid request") { ErrorCode = "OC-141" });

        if (user.OTP != dto.OTP || user.OTPExpiry == null || user.OTPExpiry < DateTime.UtcNow)
        {
            return BadRequest(new ApiResponse(false, "Invalid or expired OTP") { ErrorCode = "OC-142" });
        }

        user.Password = _hasher.HashPassword(user, dto.NewPassword);
        user.OTP = null;
        user.OTPExpiry = null;
        user.DateUpdated = DateTime.UtcNow;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();

        Log.Information("Password reset successful for {Email}", dto.Email);

        return Ok(new ApiResponse(true, "Password reset successfully") { ErrorCode = null });
    }

    private (string accessToken, string refreshToken) GenerateTokens(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new Claim("tenant_id", _legacy.FixedTenantId),
            new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "account_owner")
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

    private string GenerateAccessToken(IEnumerable<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
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