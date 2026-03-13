using BCrypt.Net;
using FluentValidation;
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
using OcufiiAPI.Validators;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace OcufiiAPI.Controllers;

[ApiController]
[Route("admin")]
[Produces("application/json")]
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
        var validator = new PlatformUserValidator();
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiResponse(false, "Validation failed")
            {
                Data = new { errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray() },
                ErrorCode = "OC-003"
            });
        }

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
            PhoneNumber = dto.PhoneNumber,
            PasswordHash = hash,
            Role = "super_admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.PlatformAdmins.Add(user);
        await _db.SaveChangesAsync();

        try
        {
            var allPlatformFeatureIds = await _db.Features
                .Where(f => f.FeatureType == FeatureType.Platform)
                .Select(f => f.Id)
                .ToListAsync();

            var providedFeatureIds = new HashSet<Guid>();
            if (dto.Features != null && dto.Features.Any())
            {
                var invalid = dto.Features
                    .Where(f => !allPlatformFeatureIds.Contains(f.FeatureId))
                    .ToList();

                if (invalid.Any())
                {
                    _db.PlatformAdmins.Remove(user);
                    await _db.SaveChangesAsync();
                    return BadRequest(new ApiResponse(false,
                        $"Invalid feature IDs (must be platform-type): {string.Join(", ", invalid.Select(x => x.FeatureId))}")
                    { ErrorCode = "OC-095" });
                }

                foreach (var f in dto.Features)
                {
                    _db.PlatformAdminFeatures.Add(new PlatformAdminFeature
                    {
                        AdminId = user.AdminId,
                        FeatureId = f.FeatureId,
                        IsEnabled = f.IsEnabled,
                        OnlyView = f.OnlyView,
                        CanEdit = f.CanEdit,
                        FullAccess = f.FullAccess,
                        CanCreate = f.CanCreate,
                        CanDelete = f.CanDelete,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });

                    providedFeatureIds.Add(f.FeatureId);
                }
            }

            var missingFeatureIds = allPlatformFeatureIds.Except(providedFeatureIds).ToList();

            foreach (var missingId in missingFeatureIds)
            {
                _db.PlatformAdminFeatures.Add(new PlatformAdminFeature
                {
                    AdminId = user.AdminId,
                    FeatureId = missingId,
                    IsEnabled = true,
                    OnlyView = false,
                    CanEdit = false,
                    FullAccess = false,
                    CanCreate = false,
                    CanDelete = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();

            return Created($"/admin/platform-users/{user.AdminId}", new ApiResponse(true, "Platform user created")
            {
                Data = new { user.AdminId, user.Email, TemporaryPassword = tempPassword },
                ErrorCode = null
            });
        }
        catch (Exception ex)
        {
            _db.PlatformAdmins.Remove(user);
            await _db.SaveChangesAsync();
            throw;
        }
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

    [Authorize(Roles = "super_admin")]
    [HttpPatch("platform-users/{adminId:guid}/permissions")]
    public async Task<ActionResult<ApiResponse>> UpdatePlatformUserPermissions(Guid adminId, [FromBody] UpdatePlatformPermissionsDto dto)
    {
        var user = await _db.PlatformAdmins.FindAsync(adminId);
        if (user == null)
            return NotFound(new ApiResponse(false, "Platform user not found") { ErrorCode = "OC-058" });

        if (user.Email.Equals("superadmin@ocufii.com", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ApiResponse(false, "Default super admin permissions cannot be modified") { ErrorCode = "OC-072" });

        try
        {
            var allPlatformFeatureIds = await _db.Features
                .Where(f => f.FeatureType == FeatureType.Platform)
                .Select(f => f.Id)
                .ToListAsync();

            var providedFeatureIds = new HashSet<Guid>();
            if (dto.Features != null)
            {
                var invalid = dto.Features
                    .Where(f => !allPlatformFeatureIds.Contains(f.FeatureId))
                    .ToList();

                if (invalid.Any())
                    return BadRequest(new ApiResponse(false,
                        $"Invalid feature IDs (must be platform-type): {string.Join(", ", invalid.Select(x => x.FeatureId))}")
                    { ErrorCode = "OC-095" });

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
                            OnlyView = f.OnlyView,
                            CanEdit = f.CanEdit,
                            FullAccess = f.FullAccess,
                            CanCreate = f.CanCreate,
                            CanDelete = f.CanDelete,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _db.PlatformAdminFeatures.Add(uf);
                    }
                    else
                    {
                        uf.IsEnabled = f.IsEnabled;
                        uf.OnlyView = f.OnlyView;
                        uf.CanEdit = f.CanEdit;
                        uf.FullAccess = f.FullAccess;
                        uf.CanCreate = f.CanCreate;
                        uf.CanDelete = f.CanDelete;
                        _db.PlatformAdminFeatures.Update(uf);
                    }

                    providedFeatureIds.Add(f.FeatureId);
                }
            }

            var missingFeatureIds = allPlatformFeatureIds.Except(providedFeatureIds).ToList();

            foreach (var missingId in missingFeatureIds)
            {
                var uf = await _db.PlatformAdminFeatures.FirstOrDefaultAsync(x => x.AdminId == adminId && x.FeatureId == missingId);
                if (uf == null)
                {
                    uf = new PlatformAdminFeature
                    {
                        AdminId = adminId,
                        FeatureId = missingId,
                        IsEnabled = true,
                        OnlyView = false,
                        CanEdit = false,
                        FullAccess = false,
                        CanCreate = false,
                        CanDelete = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.PlatformAdminFeatures.Add(uf);
                }
                else
                {
                    uf.OnlyView = false;
                    uf.CanEdit = false;
                    uf.FullAccess = false;
                    uf.CanCreate = false;
                    uf.CanDelete = false;
                    _db.PlatformAdminFeatures.Update(uf);
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
        catch (Exception ex)
        {
            throw;
        }
    }

    [Authorize(Roles = "super_admin")]
    [HttpGet("platform-users/{adminId:guid}/features")]
    public async Task<ActionResult<ApiResponse>> GetPlatformUserFeatures(Guid adminId)
    {
        var query = await _db.PlatformAdminFeatures
            .Where(paf => paf.AdminId == adminId)
            .Join(_db.Features,
                  paf => paf.FeatureId,
                  f => f.Id,
                  (paf, f) => new
                  {
                      f.Id,
                      f.Key,
                      f.Name,
                      paf.IsEnabled,
                      Rights = new
                      {
                          onlyView = paf.OnlyView,
                          canEdit = paf.CanEdit,
                          fullAccess = paf.FullAccess,
                          canCreate = paf.CanCreate,
                          canDelete = paf.CanDelete
                      }
                  })
            .ToListAsync();

        var assigned = query
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .OrderBy(x => x.Key)
            .ToList();

        return Ok(new ApiResponse(true, "Platform user features retrieved")
        {
            Data = assigned,
            ErrorCode = null
        });
    }

    // ────────────────────────────────────────────────
    // FEATURES & PERMISSIONS (shared)
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
        new { value = 0, name = "OnlyView" },
        new { value = 1, name = "CanEdit" },
        new { value = 2, name = "FullAccess" },
        new { value = 3, name = "CanCreate" },
        new { value = 4, name = "CanDelete" }
    };

        var allFeatures = await _db.Features
            .GroupBy(f => f.FeatureType)
            .Select(g => new
            {
                Type = g.Key,
                Features = g.Select(f => new
                {
                    Id = f.Id,
                    Key = f.Key,
                    Name = f.Name,
                    Description = f.Description
                }).ToList()
            })
            .ToListAsync();

        var platformFeatures = allFeatures
            .FirstOrDefault(g => g.Type == FeatureType.Platform)?
            .Features ?? new();

        var resellerFeatures = allFeatures
            .FirstOrDefault(g => g.Type == FeatureType.Reseller)?
            .Features ?? new();

        var tenantFeatures = allFeatures
            .FirstOrDefault(g => g.Type == FeatureType.Tenant)?
            .Features ?? new();

        object featuresData;

        if (userType == "platform")
        {
            if (!await _permissionService.CanPerformAsync(User, "platform_users", "onlyview"))
                return BadRequest(new ApiResponse(false, "You do not have permission to view assignable features") { ErrorCode = "OC-077" });

            featuresData = new
            {
                platformFeatures,
                resellerFeatures,
                tenantFeatures
            };
        }
        else if (userType == "reseller")
        {
            if (!await _permissionService.CanPerformAsync(User, "tenant_management", "onlyview"))
                return BadRequest(new ApiResponse(false, "You do not have permission to view assignable features") { ErrorCode = "OC-077" });

            var resellerId = User.GetResellerId();

            var allowedTenantFeatureIds = await _db.ResellerAllowedTenantFeatures
                .Where(raf => raf.ResellerId == resellerId && raf.IsEnabled)
                .Select(raf => raf.FeatureId)
                .ToListAsync();

            var allowedTenantFeatures = tenantFeatures
                .Where(f => allowedTenantFeatureIds.Contains(((dynamic)f).Id))
                .ToList();

            featuresData = new { tenantFeatures = allowedTenantFeatures };
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
                .Where(paf => paf.AdminId == adminId && paf.IsEnabled)
                .Join(_db.Features.Where(f => f.FeatureType == FeatureType.Platform),
                      paf => paf.FeatureId,
                      f => f.Id,
                      (paf, f) => new
                      {
                          f.Id,
                          f.Key,
                          f.Name,
                          Rights = new
                          {
                              onlyView = paf.OnlyView,
                              canEdit = paf.CanEdit,
                              fullAccess = paf.FullAccess,
                              canCreate = paf.CanCreate,
                              canDelete = paf.CanDelete
                          }
                      })
                .OrderBy(x => x.Key)
                .ToListAsync();
        }
        else if (userType == "reseller")
        {
            var resellerId = User.GetResellerId();
            matrix = await _db.ResellerFeatures
                .Where(rf => rf.ResellerId == resellerId && rf.IsEnabled)
                .Join(_db.Features.Where(f => f.FeatureType == FeatureType.Reseller),
                      rf => rf.FeatureId,
                      f => f.Id,
                      (rf, f) => new
                      {
                          f.Id,
                          f.Key,
                          f.Name,
                          Rights = new
                          {
                              onlyView = rf.OnlyView,
                              canEdit = rf.CanEdit,
                              fullAccess = rf.FullAccess,
                              canCreate = rf.CanCreate,
                              canDelete = rf.CanDelete
                          }
                      })
                .OrderBy(x => x.Key)
                .ToListAsync();
        }
        else
        {
            return Unauthorized(new ApiResponse(false, "Invalid user type") { ErrorCode = "OC-074" });
        }

        if (matrix == null || ((IEnumerable<object>)matrix).Any() == false)
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
                TenantCount = _db.Tenants.Count(t => t.AssignedResellerId == r.ResellerId),
                AllowedTenantFeatures = _db.ResellerAllowedTenantFeatures
                    .Where(raf => raf.ResellerId == r.ResellerId && raf.IsEnabled)
                    .Join(_db.Features, raf => raf.FeatureId, f => f.Id, (raf, f) => new
                    {
                        f.Id,
                        f.Key,
                        f.Name,
                        raf.IsEnabled
                    })
                    .ToList()
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
        var validator = new ResellerValidator();
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiResponse(false, "Validation failed")
            {
                Data = new { errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray() },
                ErrorCode = "OC-003"
            });
        }

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

        // Auto-assign default reseller features (unchanged)
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
                OnlyView = true,
                CanEdit = true,
                FullAccess = true,
                CanCreate = true,
                CanDelete = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        var allowedTenantFeatures = dto.AllowedTenantFeatures ?? new List<FeatureAssignmentDto>();

        var validTenantFeatureIds = await _db.Features
            .Where(f => f.FeatureType == FeatureType.Tenant)
            .Select(f => f.Id)
            .ToListAsync();

        if (allowedTenantFeatures.Any())
        {
            var invalidFeatures = allowedTenantFeatures
                .Where(af => !validTenantFeatureIds.Contains(af.FeatureId))
                .Select(af => af.FeatureId)
                .ToList();

            if (invalidFeatures.Any())
            {
                return BadRequest(new ApiResponse(false,
                    $"Invalid feature IDs provided for tenant allowance (must be tenant-type features): {string.Join(", ", invalidFeatures)}")
                { ErrorCode = "OC-093" });
            }

            foreach (var assignment in allowedTenantFeatures)
            {
                _db.ResellerAllowedTenantFeatures.Add(new ResellerAllowedTenantFeature
                {
                    ResellerId = reseller.ResellerId,
                    FeatureId = assignment.FeatureId,
                    IsEnabled = assignment.IsEnabled,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            var allTenantFeatures = await _db.Features
                .Where(f => f.FeatureType == FeatureType.Tenant)
                .ToListAsync();

            foreach (var f in allTenantFeatures)
            {
                _db.ResellerAllowedTenantFeatures.Add(new ResellerAllowedTenantFeature
                {
                    ResellerId = reseller.ResellerId,
                    FeatureId = f.Id,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
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

        if (dto.AllowedTenantFeatures != null)
        {
            // Remove old assignments
            var oldAssignments = await _db.ResellerAllowedTenantFeatures
                .Where(raf => raf.ResellerId == resellerId)
                .ToListAsync();
            _db.ResellerAllowedTenantFeatures.RemoveRange(oldAssignments);

            // NEW: Validate only tenant features
            var validTenantFeatureIds = await _db.Features
                .Where(f => f.FeatureType == FeatureType.Tenant)
                .Select(f => f.Id)
                .ToListAsync();

            var invalidFeatures = dto.AllowedTenantFeatures
                .Where(af => !validTenantFeatureIds.Contains(af.FeatureId))
                .Select(af => af.FeatureId)
                .ToList();

            if (invalidFeatures.Any())
            {
                return BadRequest(new ApiResponse(false,
                    $"Invalid feature IDs provided for tenant allowance (must be tenant-type features): {string.Join(", ", invalidFeatures)}")
                { ErrorCode = "OC-093" });
            }

            foreach (var assignment in dto.AllowedTenantFeatures)
            {
                _db.ResellerAllowedTenantFeatures.Add(new ResellerAllowedTenantFeature
                {
                    ResellerId = resellerId,
                    FeatureId = assignment.FeatureId,
                    IsEnabled = assignment.IsEnabled,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

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
                tenetId = t.Tenant.ResellerId,
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
                r.Email,
                AllowedTenantFeatures = _db.ResellerAllowedTenantFeatures
                    .Where(raf => raf.ResellerId == r.ResellerId && raf.IsEnabled)
                    .Join(_db.Features, raf => raf.FeatureId, f => f.Id, (raf, f) => new
                    {
                        f.Id,
                        f.Key,
                        f.Name,
                        raf.IsEnabled
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Resellers retrieved")
        {
            Data = resellers,
            ErrorCode = null
        });
    }

    [Authorize]
    [HttpPatch("me/password")]
    public async Task<ActionResult<ApiResponse>> ChangeOwnPassword([FromBody] ChangePasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CurrentPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
            return BadRequest(new ApiResponse(false, "Current and new password required") { ErrorCode = "OC-100" });

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userType = User.FindFirst("user_type")?.Value;

        if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(userType))
            return Unauthorized(new ApiResponse(false, "Invalid user") { ErrorCode = "OC-101" });

        var userId = Guid.Parse(userIdStr);

        if (userType == "platform")
        {
            var admin = await _db.PlatformAdmins.FindAsync(userId);
            if (admin == null)
                return NotFound(new ApiResponse(false, "User not found") { ErrorCode = "OC-102" });

            if (await _permissionService.IsDefaultSuperAdminAsync(User))
                return BadRequest(new ApiResponse(false, "Default super admin password cannot be changed") { ErrorCode = "OC-104" });

            var passwordVerification = _hasher.VerifyHashedPassword(admin, admin.PasswordHash, dto.CurrentPassword);
            if (passwordVerification == PasswordVerificationResult.Failed)
                return BadRequest(new ApiResponse(false, "Current password incorrect") { ErrorCode = "OC-103" });

            admin.PasswordHash = _hasher.HashPassword(admin, dto.NewPassword);
            admin.UpdatedAt = DateTime.UtcNow;
            _db.PlatformAdmins.Update(admin);
        }
        else if (userType == "reseller")
        {
            var reseller = await _db.Resellers.FindAsync(userId);
            if (reseller == null)
                return NotFound(new ApiResponse(false, "User not found") { ErrorCode = "OC-102" });

            if (await _permissionService.IsDefaultResellerAsync(User))
                return BadRequest(new ApiResponse(false, "Default reseller password cannot be changed") { ErrorCode = "OC-105" });

            var passwordVerification = _hasherReseller.VerifyHashedPassword(reseller, reseller.PasswordHash, dto.CurrentPassword);
            if (passwordVerification == PasswordVerificationResult.Failed)
                return BadRequest(new ApiResponse(false, "Current password incorrect") { ErrorCode = "OC-103" });

            reseller.PasswordHash = _hasherReseller.HashPassword(reseller, dto.NewPassword);
            reseller.UpdatedAt = DateTime.UtcNow;
            _db.Resellers.Update(reseller);
        }
        else
        {
            return Unauthorized(new ApiResponse(false, "Password change not allowed for this user type") { ErrorCode = "OC-106" });
        }

        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Password changed successfully") { ErrorCode = null });
    }
}