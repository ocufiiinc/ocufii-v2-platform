using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.DTO;
using OcufiiAPI.Enums;
using OcufiiAPI.Extensions;
using OcufiiAPI.Models;
using OcufiiAPI.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace OcufiiAPI.Controllers;

[ApiController]
[Route("admin")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse), 200)]
[ProducesResponseType(typeof(ApiResponse), 400)]
[ProducesResponseType(typeof(ApiResponse), 401)]
[ProducesResponseType(typeof(ApiResponse), 403)]
[ProducesResponseType(typeof(ApiResponse), 404)]
[ProducesResponseType(typeof(ApiResponse), 409)]
public class AdminController : ControllerBase
{
    private readonly OcufiiDbContext _db;
    private readonly PermissionService _permissionService;
    private readonly PasswordHasher<User> _userHasher = new();
    private readonly PasswordHasher<PlatformAdmin> _hasher = new();
    private readonly PasswordHasher<Reseller> _hasherReseller = new();

    private Guid GetCurrentAdminId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

    public AdminController(OcufiiDbContext db, PermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    // ────────────────────────────────────────────────
    // PLATFORM USERS MANAGEMENT (super_admin only)
    // ────────────────────────────────────────────────

    [Authorize(Roles = "super_admin")]
    [HttpGet("platform-users")]
    public async Task<ActionResult<ApiResponse>> ListPlatformUsers()
    {
        var users = await _db.PlatformAdmins
            .Select(u => new
            {
                u.AdminId,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role,
                u.IsActive,
                u.CreatedAt,
                u.LastLogin
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Platform users retrieved")
        {
            Data = users,
            ErrorCode = null
        });
    }

    [Authorize(Roles = "super_admin")]
    [HttpPost("platform-users")]
    public async Task<ActionResult<ApiResponse>> CreatePlatformUser([FromBody] CreatePlatformUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.FirstName))
            return BadRequest(new ApiResponse(false, "Email and FirstName are required") { ErrorCode = "OC-056" });

        var existing = await _db.PlatformAdmins.AnyAsync(u => u.Email == dto.Email);
        if (existing)
            return Conflict(new ApiResponse(false, "Email already in use") { ErrorCode = "OC-057" });

        var tempPassword = Guid.NewGuid().ToString("N").Substring(0, 12);
        var hash = _hasher.HashPassword(null!, tempPassword);

        var user = new PlatformAdmin
        {
            AdminId = Guid.NewGuid(),
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName ?? "",
            PasswordHash = hash,
            Role = "super_admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.PlatformAdmins.Add(user);
        await _db.SaveChangesAsync();

        // Auto-assign full features for super_admin
        var allFeatures = await _db.Features.ToListAsync();
        foreach (var f in allFeatures)
        {
            _db.PlatformAdminFeatures.Add(new PlatformAdminFeature
            {
                AdminId = user.AdminId,
                FeatureId = f.Id,
                IsEnabled = true,
                Right = FeatureRight.FullAccess,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (dto.Features != null && dto.Features.Any())
        {
            foreach (var f in dto.Features)
            {
                _db.PlatformAdminFeatures.Add(new PlatformAdminFeature
                {
                    AdminId = user.AdminId,
                    FeatureId = f.FeatureId,
                    IsEnabled = f.IsEnabled,
                    Right = (FeatureRight)f.Right,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();

        return Created($"/admin/platform-users/{user.AdminId}", new ApiResponse(true, "Platform user created")
        {
            Data = new { user.AdminId, user.Email, TemporaryPassword = tempPassword },
            ErrorCode = null
        });
    }

    [Authorize(Roles = "super_admin")]
    [HttpPatch("platform-users/{adminId:guid}/status")]
    public async Task<ActionResult<ApiResponse>> UpdatePlatformUserStatus(Guid adminId, [FromBody] AdminUpdateStatusDto dto)
    {
        var user = await _db.PlatformAdmins.FindAsync(adminId);
        if (user == null)
            return NotFound(new ApiResponse(false, "Platform user not found") { ErrorCode = "OC-058" });

        if (user.Email.Equals("superadmin@ocufii.com", StringComparison.OrdinalIgnoreCase) && !dto.IsActive)
            return BadRequest(new ApiResponse(false, "Default super admin cannot be deactivated") { ErrorCode = "OC-066" });

        user.IsActive = dto.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        _db.PlatformAdmins.Update(user);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, $"Platform user {(dto.IsActive ? "activated" : "deactivated")}")
        {
            ErrorCode = null
        });
    }

    [Authorize(Roles = "super_admin")]
    [HttpDelete("platform-users/{adminId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeletePlatformUser(Guid adminId)
    {
        var user = await _db.PlatformAdmins.FindAsync(adminId);
        if (user == null)
            return NotFound(new ApiResponse(false, "Platform user not found") { ErrorCode = "OC-058" });

        if (user.Email.Equals("superadmin@ocufii.com", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ApiResponse(false, "Default super admin cannot be deleted") { ErrorCode = "OC-066" });

        _db.PlatformAdmins.Remove(user);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Platform user permanently deleted")
        {
            ErrorCode = null
        });
    }

    // ────────────────────────────────────────────────
    // RESELLER MANAGEMENT (super_admin only)
    // ────────────────────────────────────────────────

    [Authorize(Roles = "super_admin")]
    [HttpGet("resellers")]
    public async Task<ActionResult<ApiResponse>> ListResellers()
    {
        var resellers = await _db.Resellers
            .Select(r => new
            {
                r.ResellerId,
                r.Name,
                r.Email,
                r.IsActive,
                TenantCount = _db.Tenants.Count(t => t.AssignedResellerId == r.ResellerId)
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Resellers retrieved")
        {
            Data = resellers,
            ErrorCode = null
        });
    }

    [Authorize(Roles = "super_admin")]
    [HttpPost("resellers")]
    public async Task<ActionResult<ApiResponse>> CreateReseller([FromBody] CreateResellerDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new ApiResponse(false, "Name and Email required") { ErrorCode = "OC-059" });

        var existing = await _db.Resellers.AnyAsync(r => r.Email == dto.Email);
        if (existing)
            return Conflict(new ApiResponse(false, "Email already in use") { ErrorCode = "OC-060" });

        var tempPassword = Guid.NewGuid().ToString("N").Substring(0, 12);
        var hash = _hasherReseller.HashPassword(null!, tempPassword);

        var reseller = new Reseller
        {
            ResellerId = Guid.NewGuid(),
            Name = dto.Name,
            Email = dto.Email,
            ContactName = dto.ContactName,
            PhoneNumber = dto.PhoneNumber,
            PasswordHash = hash,
            Role = "reseller_admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Resellers.Add(reseller);
        await _db.SaveChangesAsync();

        // Auto-assign default features
        var defaultKeys = new[] { "tenant_management", "reporting" };
        var features = await _db.Features
            .Where(f => defaultKeys.Contains(f.Key))
            .ToListAsync();

        foreach (var feature in features)
        {
            _db.ResellerFeatures.Add(new ResellerFeature
            {
                ResellerId = reseller.ResellerId,
                FeatureId = feature.Id,
                IsEnabled = true,
                Right = FeatureRight.FullAccess,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        return Created($"/admin/resellers/{reseller.ResellerId}", new ApiResponse(true, "Reseller created")
        {
            Data = new { reseller.ResellerId, reseller.Name, TemporaryPassword = tempPassword },
            ErrorCode = null
        });
    }

    [Authorize(Roles = "super_admin")]
    [HttpPatch("resellers/{resellerId:guid}/status")]
    public async Task<ActionResult<ApiResponse>> UpdateResellerStatus(Guid resellerId, [FromBody] AdminUpdateStatusDto dto)
    {
        var reseller = await _db.Resellers.FindAsync(resellerId);
        if (reseller == null)
            return NotFound(new ApiResponse(false, "Reseller not found") { ErrorCode = "OC-061" });

        if (reseller.Email.Equals("defaultReseller@ocufii.com", StringComparison.OrdinalIgnoreCase) && !dto.IsActive)
            return BadRequest(new ApiResponse(false, "Default Ocufii Direct cannot be deactivated") { ErrorCode = "OC-067" });

        reseller.IsActive = dto.IsActive;
        reseller.UpdatedAt = DateTime.UtcNow;
        _db.Resellers.Update(reseller);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, $"Reseller {(dto.IsActive ? "activated" : "deactivated")}")
        {
            ErrorCode = null
        });
    }

    [Authorize(Roles = "super_admin")]
    [HttpPatch("resellers/{resellerId:guid}")]
    public async Task<ActionResult<ApiResponse>> UpdateReseller(Guid resellerId, [FromBody] UpdateResellerDto dto)
    {
        var reseller = await _db.Resellers.FindAsync(resellerId);
        if (reseller == null)
            return NotFound(new ApiResponse(false, "Reseller not found") { ErrorCode = "OC-061" });

        if (dto.Name != null) reseller.Name = dto.Name;
        if (dto.ContactName != null) reseller.ContactName = dto.ContactName;
        if (dto.PhoneNumber != null) reseller.PhoneNumber = dto.PhoneNumber;

        reseller.UpdatedAt = DateTime.UtcNow;
        _db.Resellers.Update(reseller);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Reseller updated")
        {
            ErrorCode = null
        });
    }

    [Authorize(Roles = "super_admin")]
    [HttpDelete("resellers/{resellerId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteReseller(Guid resellerId)
    {
        var reseller = await _db.Resellers.FindAsync(resellerId);
        if (reseller == null)
            return NotFound(new ApiResponse(false, "Reseller not found") { ErrorCode = "OC-061" });

        if (reseller.Email.Equals("defaultReseller@ocufii.com", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ApiResponse(false, "Default Ocufii Direct reseller cannot be deleted") { ErrorCode = "OC-067" });

        var hasTenants = await _db.Tenants.AnyAsync(t => t.AssignedResellerId == resellerId);
        if (hasTenants)
        {
            reseller.IsActive = false;
            reseller.UpdatedAt = DateTime.UtcNow;
            _db.Resellers.Update(reseller);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse(true, "Reseller deactivated (has tenants). Move or delete tenants first to hard delete.")
            {
                ErrorCode = "OC-076"
            });
        }

        _db.Resellers.Remove(reseller);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Reseller permanently deleted")
        {
            ErrorCode = null
        });
    }

    // ────────────────────────────────────────────────
    // TENANT MANAGEMENT (super_admin only for global view/move)
    // ────────────────────────────────────────────────

    [Authorize(Roles = "super_admin")]
    [HttpPatch("tenants/{tenantId:guid}/reseller")]
    public async Task<ActionResult<ApiResponse>> MoveTenantToReseller(Guid tenantId, [FromBody] MoveTenantDto dto)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null)
            return NotFound(new ApiResponse(false, "Tenant not found") { ErrorCode = "OC-062" });

        var newReseller = await _db.Resellers.FindAsync(dto.NewResellerId);
        if (newReseller == null)
            return NotFound(new ApiResponse(false, "New reseller not found") { ErrorCode = "OC-063" });

        tenant.AssignedResellerId = dto.NewResellerId;
        tenant.DateUpdated = DateTime.UtcNow;
        _db.Tenants.Update(tenant);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Tenant reassigned")
        {
            Data = new { tenantId, newResellerId = dto.NewResellerId },
            ErrorCode = null
        });
    }

    [Authorize(Roles = "super_admin")]
    [HttpGet("tenants")]
    public async Task<ActionResult<ApiResponse>> ListAllTenants()
    {
        var tenants = await _db.Tenants
            .Include(t => t.AssignedReseller)
            .GroupJoin(_db.Users.Where(u => u.Role.RoleName == "account_owner"),
                t => t.ResellerId,
                u => u.TenantId,
                (t, owners) => new { Tenant = t, Owners = owners })
            .SelectMany(t => t.Owners.DefaultIfEmpty(), (t, owner) => new
            {
                t.Tenant.ResellerId,
                OwnerEmail = owner != null ? owner.Email : "No owner assigned",
                CurrentResellerId = t.Tenant.AssignedResellerId,
                CurrentResellerName = t.Tenant.AssignedReseller != null ? t.Tenant.AssignedReseller.Name : "Unassigned"
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "All tenants retrieved")
        {
            Data = tenants,
            ErrorCode = null
        });
    }

    [Authorize(Roles = "super_admin")]
    [HttpGet("resellers/list")]
    public async Task<ActionResult<ApiResponse>> ListResellersForMove()
    {
        var resellers = await _db.Resellers
            .Where(r => r.IsActive)
            .Select(r => new
            {
                r.ResellerId,
                r.Name,
                r.Email
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Resellers retrieved")
        {
            Data = resellers,
            ErrorCode = null
        });
    }

    // ────────────────────────────────────────────────
    // FEATURES & PERMISSIONS
    // ────────────────────────────────────────────────

    [Authorize]
    [HttpGet("features")]
    public async Task<ActionResult<ApiResponse>> GetAssignableFeatures()
    {
        var userType = User.FindFirst("user_type")?.Value;
        if (string.IsNullOrEmpty(userType))
            return Unauthorized(new ApiResponse(false, "Invalid user type") { ErrorCode = "OC-074" });

        var possibleRights = new[]
        {
            new { value = (int)FeatureRight.OnlyView, name = "OnlyView" },
            new { value = (int)FeatureRight.CanEdit, name = "CanEdit" },
            new { value = (int)FeatureRight.FullAccess, name = "FullAccess" },
            new { value = (int)FeatureRight.CanCreate, name = "CanCreate" },
            new { value = (int)FeatureRight.CanDelete, name = "CanDelete" }
        };

        object featuresData;

        if (userType == "platform")
        {
            if (!await _permissionService.CanPerformAsync(User, "platform_users", FeatureRight.OnlyView))
                return BadRequest(new ApiResponse(false, "You do not have permission to view assignable features") { ErrorCode = "OC-077" });

            featuresData = await _db.Features
                .Select(f => new
                {
                    f.Id,
                    f.Key,
                    f.Name,
                    f.Description
                })
                .ToListAsync();
        }
        else if (userType == "reseller")
        {
            if (!await _permissionService.CanPerformAsync(User, "tenant_management", FeatureRight.OnlyView))
                return BadRequest(new ApiResponse(false, "You do not have permission to view assignable features") { ErrorCode = "OC-077" });

            var resellerId = User.GetResellerId();

            featuresData = await _db.ResellerFeatures
                .Where(rf => rf.ResellerId == resellerId && rf.IsEnabled)
                .Join(_db.Features, rf => rf.FeatureId, f => f.Id, (rf, f) => new
                {
                    f.Id,
                    f.Key,
                    f.Name,
                    f.Description,
                    assignableRight = rf.Right
                })
                .ToListAsync();

            if (!((IEnumerable<object>)featuresData).Any())
                return Ok(new ApiResponse(true, "No features assigned to your reseller account")
                {
                    Data = new { features = Array.Empty<object>(), possibleRights },
                    ErrorCode = "OC-085"
                });
        }
        else
        {
            return Unauthorized(new ApiResponse(false, "Invalid user type") { ErrorCode = "OC-074" });
        }

        return Ok(new ApiResponse(true, "Assignable features retrieved")
        {
            Data = new { features = featuresData, possibleRights },
            ErrorCode = null
        });
    }

    [Authorize]
    [HttpGet("me/features-matrix")]
    public async Task<ActionResult<ApiResponse>> GetMyFeaturesMatrix()
    {
        var userType = User.FindFirst("user_type")?.Value;
        if (string.IsNullOrEmpty(userType))
            return Unauthorized(new ApiResponse(false, "Invalid user type") { ErrorCode = "OC-074" });

        object matrix;

        if (userType == "platform")
        {
            var adminId = GetCurrentAdminId();
            matrix = await _db.PlatformAdminFeatures
                .Where(paf => paf.AdminId == adminId)
                .Join(_db.Features, paf => paf.FeatureId, f => f.Id, (paf, f) => new
                {
                    f.Id,
                    f.Key,
                    f.Name,
                    Rights = new
                    {
                        OnlyView = paf.Right == FeatureRight.OnlyView,
                        CanEdit = paf.Right == FeatureRight.CanEdit,
                        FullAccess = paf.Right == FeatureRight.FullAccess,
                        CanCreate = paf.Right == FeatureRight.CanCreate,
                        CanDelete = paf.Right == FeatureRight.CanDelete
                    }
                })
                .ToListAsync();
        }
        else if (userType == "reseller")
        {
            var resellerId = User.GetResellerId();
            matrix = await _db.ResellerFeatures
                .Where(rf => rf.ResellerId == resellerId && rf.IsEnabled)
                .Join(_db.Features, rf => rf.FeatureId, f => f.Id, (rf, f) => new
                {
                    f.Key,
                    f.Name,
                    Rights = new
                    {
                        OnlyView = rf.Right == FeatureRight.OnlyView,
                        CanEdit = rf.Right == FeatureRight.CanEdit,
                        FullAccess = rf.Right == FeatureRight.FullAccess,
                        CanCreate = rf.Right == FeatureRight.CanCreate,
                        CanDelete = rf.Right == FeatureRight.CanDelete
                    }
                })
                .ToListAsync();
        }
        else
        {
            return Unauthorized(new ApiResponse(false, "Invalid user type") { ErrorCode = "OC-074" });
        }

        if (!((IEnumerable<object>)matrix).Any())
            return Ok(new ApiResponse(true, "No features assigned")
            {
                Data = new { matrix = Array.Empty<object>() },
                ErrorCode = "OC-075"
            });

        return Ok(new ApiResponse(true, "My features matrix retrieved")
        {
            Data = matrix,
            ErrorCode = null
        });
    }

    [Authorize(Roles = "super_admin")]
    [HttpGet("platform-users/{adminId:guid}/features")]
    public async Task<ActionResult<ApiResponse>> GetPlatformUserFeatures(Guid adminId)
    {
        var features = await _db.PlatformAdminFeatures
            .Where(paf => paf.AdminId == adminId)
            .Join(_db.Features, paf => paf.FeatureId, f => f.Id, (paf, f) => new
            {
                f.Key,
                f.Name,
                paf.IsEnabled,
                Right = paf.Right.ToString()
            })
            .ToListAsync();

        if (!features.Any())
            return Ok(new ApiResponse(true, "No features assigned to this platform user")
            {
                Data = new { features = Array.Empty<object>() },
                ErrorCode = "OC-075"
            });

        return Ok(new ApiResponse(true, "Platform user features retrieved")
        {
            Data = features,
            ErrorCode = null
        });
    }

    [Authorize(Roles = "super_admin")]
    [HttpPatch("platform-users/{adminId:guid}/permissions")]
    public async Task<ActionResult<ApiResponse>> UpdatePlatformUserPermissions(Guid adminId, [FromBody] UpdatePlatformPermissionsDto dto)
    {
        var user = await _db.PlatformAdmins.FindAsync(adminId);
        if (user == null)
            return NotFound(new ApiResponse(false, "Platform user not found") { ErrorCode = "OC-058" });

        if (user.Email.Equals("superadmin@ocufii.com", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ApiResponse(false, "Default super admin permissions cannot be modified") { ErrorCode = "OC-072" });

        if (dto.Role != null)
        {
            if (dto.Role != "super_admin")
                return BadRequest(new ApiResponse(false, "Only 'super_admin' role is allowed") { ErrorCode = "OC-065" });

            user.Role = dto.Role;
        }

        if (dto.Features != null)
        {
            foreach (var f in dto.Features)
            {
                var uf = await _db.PlatformAdminFeatures.FirstOrDefaultAsync(x => x.AdminId == adminId && x.FeatureId == f.FeatureId);
                if (uf == null)
                {
                    uf = new PlatformAdminFeature
                    {
                        AdminId = adminId,
                        FeatureId = f.FeatureId,
                        IsEnabled = f.IsEnabled,
                        Right = (FeatureRight)f.Right,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.PlatformAdminFeatures.Add(uf);
                }
                else
                {
                    uf.IsEnabled = f.IsEnabled;
                    uf.Right = (FeatureRight)f.Right;
                    _db.PlatformAdminFeatures.Update(uf);
                }
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        _db.PlatformAdmins.Update(user);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Permissions updated successfully")
        {
            ErrorCode = null
        });
    }

    // ────────────────────────────────────────────────
    // RESELLER TENANT MANAGEMENT (reseller_admin only)
    // ────────────────────────────────────────────────

    [Authorize(Roles = "reseller_admin")]
    [HttpGet("my-tenants")]
    public async Task<ActionResult<ApiResponse>> ListMyTenants()
    {
        var resellerId = User.GetResellerId();
        var tenants = await _db.Tenants
            .Where(t => t.AssignedResellerId == resellerId)
            .Select(t => new
            {
                t.ResellerId,
                t.DateCreated,
                t.DateUpdated,
                t.IsActive,
                Status = t.IsActive ? "Active" : "Inactive"
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "My tenants retrieved")
        {
            Data = tenants,
            ErrorCode = null
        });
    }

    [Authorize(Roles = "reseller_admin")]
    [HttpPost("my-tenants")]
    public async Task<ActionResult<ApiResponse>> CreateTenantAsReseller([FromBody] CreateTenantDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.OwnerEmail))
            return BadRequest(new ApiResponse(false, "Email is required") { ErrorCode = "OC-080" });

        dto.OwnerEmail = dto.OwnerEmail.Trim().ToLowerInvariant();

        var userExists = await _db.Users
            .AnyAsync(u => u.Email == dto.OwnerEmail && !u.IsDeleted);

        if (userExists)
            return Conflict(new ApiResponse(false, "Email already in use") { ErrorCode = "OC-081" });

        var resellerId = User.GetResellerId();

        var tenant = new Tenant
        {
            ResellerId = Guid.NewGuid(),
            AssignedResellerId = resellerId,
            DateCreated = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            ThemeConfig = "{}",
            CustomWorkflows = "[]",
            IsActive = true
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var tempPassword = Guid.NewGuid().ToString("N").Substring(0, 12);
        var hash = _userHasher.HashPassword(null!, tempPassword);

        var owner = new User
        {
            UserId = Guid.NewGuid(),
            Email = dto.OwnerEmail,
            FirstName = dto.OwnerFirstName,
            LastName = dto.OwnerLastName ?? "",
            Password = hash,
            RoleId = (await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "account_owner"))!.RoleId,
            TenantId = tenant.ResellerId,
            IsEnabled = true,
            IsDeleted = false,
            DateSubmitted = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow
        };

        _db.Users.Add(owner);
        await _db.SaveChangesAsync();

        // TODO: Send email with temp password

        return Created($"/admin/my-tenants/{tenant.ResellerId}", new ApiResponse(true, "Tenant and account owner created")
        {
            Data = new { tenant.ResellerId, owner.UserId, TemporaryPassword = tempPassword },
            ErrorCode = null
        });
    }

    [Authorize(Roles = "reseller_admin")]
    [HttpPatch("my-tenants/{tenantId:guid}")]
    public async Task<ActionResult<ApiResponse>> UpdateTenant(Guid tenantId, [FromBody] UpdateTenantDto dto)
    {
        var resellerId = User.GetResellerId();
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.ResellerId == tenantId && t.AssignedResellerId == resellerId);
        if (tenant == null)
            return NotFound(new ApiResponse(false, "Tenant not found") { ErrorCode = "OC-062" });

        var owner = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Role.RoleName == "account_owner");
        if (owner != null && dto.Name != null)
        {
            var names = dto.Name.Split(' ');
            owner.FirstName = names[0];
            owner.LastName = names.Length > 1 ? names[1] : "";
            owner.DateUpdated = DateTime.UtcNow;
            _db.Users.Update(owner);
        }

        tenant.DateUpdated = DateTime.UtcNow;
        _db.Tenants.Update(tenant);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Tenant updated")
        {
            ErrorCode = null
        });
    }

    [Authorize(Roles = "reseller_admin")]
    [HttpPatch("my-tenants/{tenantId:guid}/status")]
    public async Task<ActionResult<ApiResponse>> UpdateTenantStatus(Guid tenantId, [FromBody] AdminUpdateStatusDto dto)
    {
        var resellerId = User.GetResellerId();
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.ResellerId == tenantId && t.AssignedResellerId == resellerId);
        if (tenant == null)
            return NotFound(new ApiResponse(false, "Tenant not found") { ErrorCode = "OC-062" });

        tenant.IsActive = dto.IsActive;
        tenant.DateUpdated = DateTime.UtcNow;
        _db.Tenants.Update(tenant);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, $"Tenant {(dto.IsActive ? "activated" : "deactivated")}")
        {
            ErrorCode = null
        });
    }

    [Authorize(Roles = "reseller_admin")]
    [HttpDelete("my-tenants/{tenantId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteTenant(Guid tenantId)
    {
        var resellerId = User.GetResellerId();
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.ResellerId == tenantId && t.AssignedResellerId == resellerId);
        if (tenant == null)
            return NotFound(new ApiResponse(false, "Tenant not found") { ErrorCode = "OC-062" });

        tenant.IsActive = false;
        tenant.DateUpdated = DateTime.UtcNow;
        _db.Tenants.Update(tenant);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Tenant deactivated")
        {
            ErrorCode = null
        });
    }
}