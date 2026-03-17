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
using System.Security.Claims;

namespace OcufiiAPI.Controllers
{
    [ApiController]
    [Route("admin")]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly OcufiiDbContext _db;
        private readonly PermissionService _permissionService;
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
            if (!await _permissionService.CanPerformAsync(User, "platform_users.view"))
                return Unauthorized(new ApiResponse(false, "No permission to view platform users") { ErrorCode = "OC-107" });

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
                    u.LastLogin,
                    Permissions = _db.PlatformPermissions
                        .Where(pp => pp.AdminId == u.AdminId)
                        .Join(_db.Permissions, pp => pp.PermissionId, p => p.PermissionId, (pp, p) => new { p.PermissionId, p.Key, p.Name, pp.IsGranted })
                        .ToList()
                })
                .ToListAsync();

            return Ok(new ApiResponse(true, "Platform users retrieved")
            {
                Data = users,
                ErrorCode = null
            });
        }

        [Authorize(Roles = "super_admin")]
        [HttpGet("permissions")]
        public async Task<ActionResult<ApiResponse>> GetAssignablePermissions()
        {
            if (!await _permissionService.CanPerformAsync(User, "platform_users.view"))
                return Unauthorized(new ApiResponse(false, "No permission to view assignable permissions") { ErrorCode = "OC-077" });

            var userType = User.FindFirst("user_type")?.Value ?? "platform";

            var permissions = await _db.Permissions
                .Where(p => p.Category == userType)
                .Select(p => new
                {
                    p.PermissionId,
                    p.Key,
                    p.Name,
                    p.Description,
                    p.IsDefault
                })
                .OrderBy(p => p.Key)
                .ToListAsync();

            return Ok(new ApiResponse(true, "Assignable permissions retrieved")
            {
                Data = permissions,
                ErrorCode = null
            });
        }

        [Authorize(Roles = "super_admin")]
        [HttpGet("reseller-assignable-Lists")]
        public async Task<ActionResult<ApiResponse>> GetResellerAssignableLists()
        {
            if (!await _permissionService.CanPerformAsync(User, "resellers.create"))
                return Unauthorized(new ApiResponse(false, "No permission to view reseller assignable features") { ErrorCode = "OC-131" });

            var featuresByType = await _db.Features
                .Where(f => f.FeatureType == FeatureType.Reseller || f.FeatureType == FeatureType.PlanGated)
                .GroupBy(f => f.FeatureType)
                .Select(g => new
                {
                    FeatureType = g.Key.ToString(),
                    Items = g.Select(f => new
                    {
                        f.Id,
                        f.Key,
                        f.Name,
                        f.Description
                    }).OrderBy(f => f.Key).ToList()
                })
                .OrderBy(g => g.FeatureType)
                .ToListAsync();

            var permissionsByCategory = await _db.Permissions
                .Where(p => p.Category == "reseller" || p.Category == "account")
                .GroupBy(p => p.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Items = g.Select(p => new
                    {
                        p.PermissionId,
                        p.Key,
                        p.Name,
                        p.Description,
                        p.IsDefault
                    }).OrderBy(p => p.Key).ToList()
                })
                .OrderBy(g => g.Category)
                .ToListAsync();

            return Ok(new ApiResponse(true, "Reseller assignable items retrieved")
            {
                Data = new
                {
                    FeaturesByType = featuresByType,
                    PermissionsByCategory = permissionsByCategory
                },
                ErrorCode = null
            });
        }

        [Authorize(Roles = "super_admin")]
        [HttpGet("system-permissions")]
        public async Task<ActionResult<ApiResponse>> GetSystemPermissions()
        {
            if (!await _permissionService.CanPerformAsync(User, "rolepermissions.edit"))
                return Unauthorized(new ApiResponse(false, "No permission to view system permissions") { ErrorCode = "OC-128" });

            var permissions = await _db.Permissions
                .Select(p => new
                {
                    p.PermissionId,
                    p.Key,
                    p.Name,
                    p.Description,
                    p.Category,
                    p.IsDefault
                })
                .OrderBy(p => p.Key)
                .ToListAsync();

            return Ok(new ApiResponse(true, "System permissions retrieved")
            {
                Data = permissions,
                ErrorCode = null
            });
        }

        [Authorize(Roles = "super_admin")]
        [HttpPatch("system-permissions/{permissionId:guid}")]
        public async Task<ActionResult<ApiResponse>> UpdateSystemPermission(Guid permissionId, [FromBody] UpdateSystemPermissionDto dto)
        {
            if (!await _permissionService.CanPerformAsync(User, "rolepermissions.edit"))
                return Unauthorized(new ApiResponse(false, "No permission to update system permissions") { ErrorCode = "OC-129" });

            var permission = await _db.Permissions.FindAsync(permissionId);
            if (permission == null)
                return NotFound(new ApiResponse(false, "Permission not found") { ErrorCode = "OC-130" });

            if (dto.IsDefault.HasValue)
                permission.IsDefault = dto.IsDefault.Value;

            permission.UpdatedAt = DateTime.UtcNow;
            _db.Permissions.Update(permission);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse(true, "System permission updated")
            {
                ErrorCode = null
            });
        }

        [Authorize(Roles = "super_admin")]
        [HttpPost("platform-users")]
        public async Task<ActionResult<ApiResponse>> CreatePlatformUser([FromBody] CreatePlatformUserDto dto)
        {
            if (!await _permissionService.CanPerformAsync(User, "platform_users.create"))
                return Unauthorized(new ApiResponse(false, "No permission to create platform users") { ErrorCode = "OC-108" });

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
                if (dto.Permissions != null && dto.Permissions.Any())
                {
                    var invalidPermissions = dto.Permissions
                        .Where(p => !_db.Permissions.Any(perm => perm.PermissionId == p.PermissionId && perm.Category == "platform"))
                        .ToList();

                    if (invalidPermissions.Any())
                    {
                        _db.PlatformAdmins.Remove(user);
                        await _db.SaveChangesAsync();
                        return BadRequest(new ApiResponse(false, "Platform admins can only be assigned permissions from 'platform' category") { ErrorCode = "OC-132" });
                    }

                    foreach (var p in dto.Permissions)
                    {
                        _db.PlatformPermissions.Add(new PlatformPermission
                        {
                            AdminId = user.AdminId,
                            PermissionId = p.PermissionId,
                            IsGranted = p.IsGranted,
                            GrantedByAdminId = GetCurrentAdminId(),
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
            catch (Exception)
            {
                _db.PlatformAdmins.Remove(user);
                await _db.SaveChangesAsync();
                throw;
            }
        }

        [Authorize(Roles = "super_admin")]
        [HttpPatch("platform-users/{adminId:guid}/permissions")]
        public async Task<ActionResult<ApiResponse>> UpdatePlatformUserPermissions(Guid adminId, [FromBody] UpdatePlatformPermissionsDto dto)
        {
            if (!await _permissionService.CanPerformAsync(User, "platform_users.edit"))
                return Unauthorized(new ApiResponse(false, "No permission to update platform user permissions") { ErrorCode = "OC-111" });

            var user = await _db.PlatformAdmins.FindAsync(adminId);
            if (user == null)
                return NotFound(new ApiResponse(false, "Platform user not found") { ErrorCode = "OC-058" });

            if (user.Email.Equals("superadmin@ocufii.com", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new ApiResponse(false, "Default super admin permissions cannot be modified") { ErrorCode = "OC-072" });

            try
            {
                if (dto.Permissions != null)
                {
                    var oldPermissions = await _db.PlatformPermissions
                        .Where(pp => pp.AdminId == adminId)
                        .ToListAsync();

                    _db.PlatformPermissions.RemoveRange(oldPermissions);

                    var invalidPermissions = dto.Permissions
                        .Where(p => !_db.Permissions.Any(perm => perm.PermissionId == p.PermissionId && perm.Category == "platform"))
                        .ToList();

                    if (invalidPermissions.Any())
                    {
                        return BadRequest(new ApiResponse(false, "Platform admins can only be assigned permissions from 'platform' category") { ErrorCode = "OC-132" });
                    }

                    foreach (var p in dto.Permissions)
                    {
                        _db.PlatformPermissions.Add(new PlatformPermission
                        {
                            AdminId = adminId,
                            PermissionId = p.PermissionId,
                            IsGranted = p.IsGranted,
                            GrantedByAdminId = GetCurrentAdminId(),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
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
            catch (Exception)
            {
                throw;
            }
        }

        [Authorize(Roles = "super_admin")]
        [HttpDelete("platform-users/{adminId:guid}")]
        public async Task<ActionResult<ApiResponse>> DeletePlatformUser(Guid adminId)
        {
            if (!await _permissionService.CanPerformAsync(User, "platform_users.delete"))
                return Unauthorized(new ApiResponse(false, "No permission to delete platform users") { ErrorCode = "OC-110" });

            var user = await _db.PlatformAdmins.FindAsync(adminId);
            if (user == null)
                return NotFound(new ApiResponse(false, "Platform user not found") { ErrorCode = "OC-058" });

            if (user.Email.Equals("superadmin@ocufii.com", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new ApiResponse(false, "Default super admin cannot be deleted") { ErrorCode = "OC-066" });

            // Optional: check if user has dependencies (e.g. created resellers)
            // var hasDependencies = await _db.Resellers.AnyAsync(r => r.CreatedByAdminId == adminId);
            // if (hasDependencies) return BadRequest("Cannot delete - user has associated data");

            _db.PlatformAdmins.Remove(user);
            await _db.SaveChangesAsync();

            return Ok(new ApiResponse(true, "Platform user permanently deleted")
            {
                ErrorCode = null
            });
        }

        [Authorize(Roles = "super_admin")]
        [HttpGet("resellers")]
        public async Task<ActionResult<ApiResponse>> ListResellers()
        {
            if (!await _permissionService.CanPerformAsync(User, "resellers.view"))
                return Unauthorized(new ApiResponse(false, "No permission to view resellers") { ErrorCode = "OC-113" });

            var resellers = await _db.Resellers
                .Select(r => new
                {
                    r.ResellerId,
                    r.Name,
                    r.Email,
                    r.IsActive,
                    TenantCount = _db.Tenants.Count(t => t.AssignedResellerId == r.ResellerId),
                    Permissions = _db.ResellerPermissions
                        .Where(rp => rp.ResellerId == r.ResellerId)
                        .Join(_db.Permissions, rp => rp.PermissionId, p => p.PermissionId, (rp, p) => new { p.Key, rp.IsGranted })
                        .ToList(),
                    Features = _db.ResellerFeatures
                        .Where(rf => rf.ResellerId == r.ResellerId)
                        .Join(_db.Features, rf => rf.FeatureId, f => f.Id, (rf, f) => new { f.Key, rf.IsEnabled })
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
            if (!await _permissionService.CanPerformAsync(User, "resellers.create"))
                return Unauthorized(new ApiResponse(false, "No permission to create resellers") { ErrorCode = "OC-114" });

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

            try
            {
                if (dto.Permissions != null && dto.Permissions.Any())
                {
                    var permissionIds = dto.Permissions.Select(p => p.PermissionId).Distinct().ToList();

                    var validPermissionCount = await _db.Permissions
                        .CountAsync(p => permissionIds.Contains(p.PermissionId) &&
                                         (p.Category == "reseller" || p.Category == "account"));

                    if (validPermissionCount != permissionIds.Count)
                    {
                        _db.Resellers.Remove(reseller);
                        await _db.SaveChangesAsync();
                        return BadRequest(new ApiResponse(false, "One or more permission IDs are invalid or from disallowed category (only reseller or account allowed)") { ErrorCode = "OC-135" });
                    }

                    foreach (var p in dto.Permissions)
                    {
                        _db.ResellerPermissions.Add(new ResellerPermission
                        {
                            ResellerId = reseller.ResellerId,
                            PermissionId = p.PermissionId,
                            IsGranted = p.IsGranted,
                            GrantedByAdminId = GetCurrentAdminId(),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Validate & Assign Features
                if (dto.Features != null && dto.Features.Any())
                {
                    var featureIds = dto.Features.Select(f => f.FeatureId).Distinct().ToList();

                    var validFeatureCount = await _db.Features
                        .CountAsync(f => featureIds.Contains(f.Id) &&
                                         (f.FeatureType == FeatureType.Reseller || f.FeatureType == FeatureType.PlanGated));

                    if (validFeatureCount != featureIds.Count)
                    {
                        _db.Resellers.Remove(reseller);
                        await _db.SaveChangesAsync();
                        return BadRequest(new ApiResponse(false, "One or more feature IDs are invalid or from disallowed type (only Reseller or Plan-Gated allowed)") { ErrorCode = "OC-136" });
                    }

                    foreach (var f in dto.Features)
                    {
                        _db.ResellerFeatures.Add(new ResellerFeature
                        {
                            ResellerId = reseller.ResellerId,
                            FeatureId = f.FeatureId,
                            IsEnabled = f.IsEnabled,
                            GrantedByAdminId = GetCurrentAdminId(),
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
            catch (Exception)
            {
                _db.Resellers.Remove(reseller);
                await _db.SaveChangesAsync();
                throw;
            }
        }

        [Authorize(Roles = "super_admin")]
        [HttpPatch("resellers/{resellerId:guid}/status")]
        public async Task<ActionResult<ApiResponse>> UpdateResellerStatus(Guid resellerId, [FromBody] AdminUpdateStatusDto dto)
        {
            if (!await _permissionService.CanPerformAsync(User, "resellers.edit"))
                return Unauthorized(new ApiResponse(false, "No permission to update reseller status") { ErrorCode = "OC-115" });

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
            if (!await _permissionService.CanPerformAsync(User, "resellers.edit"))
                return Unauthorized(new ApiResponse(false, "No permission to update reseller") { ErrorCode = "OC-116" });

            var reseller = await _db.Resellers.FindAsync(resellerId);
            if (reseller == null)
                return NotFound(new ApiResponse(false, "Reseller not found") { ErrorCode = "OC-061" });

            if (dto.Name != null) reseller.Name = dto.Name;
            if (dto.ContactName != null) reseller.ContactName = dto.ContactName;
            if (dto.PhoneNumber != null) reseller.PhoneNumber = dto.PhoneNumber;

            try
            {
                // Validate & Update Permissions (if provided)
                if (dto.Permissions != null)
                {
                    var permissionIds = dto.Permissions.Select(p => p.PermissionId).Distinct().ToList();

                    var validPermissionCount = await _db.Permissions
                        .CountAsync(p => permissionIds.Contains(p.PermissionId) &&
                                         (p.Category == "reseller" || p.Category == "account"));

                    if (validPermissionCount != permissionIds.Count)
                    {
                        return BadRequest(new ApiResponse(false, "One or more permission IDs are invalid or from disallowed category (only reseller or account allowed)") { ErrorCode = "OC-135" });
                    }

                    var oldPermissions = await _db.ResellerPermissions
                        .Where(rp => rp.ResellerId == resellerId)
                        .ToListAsync();

                    _db.ResellerPermissions.RemoveRange(oldPermissions);

                    foreach (var p in dto.Permissions)
                    {
                        _db.ResellerPermissions.Add(new ResellerPermission
                        {
                            ResellerId = resellerId,
                            PermissionId = p.PermissionId,
                            IsGranted = p.IsGranted,
                            GrantedByAdminId = GetCurrentAdminId(),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Validate & Update Features (if provided)
                if (dto.Features != null)
                {
                    var featureIds = dto.Features.Select(f => f.FeatureId).Distinct().ToList();

                    var validFeatureCount = await _db.Features
                        .CountAsync(f => featureIds.Contains(f.Id) &&
                                         (f.FeatureType == FeatureType.Reseller || f.FeatureType == FeatureType.PlanGated));

                    if (validFeatureCount != featureIds.Count)
                    {
                        return BadRequest(new ApiResponse(false, "One or more feature IDs are invalid or from disallowed type (only Reseller or Plan-Gated allowed)") { ErrorCode = "OC-136" });
                    }

                    var oldFeatures = await _db.ResellerFeatures
                        .Where(rf => rf.ResellerId == resellerId)
                        .ToListAsync();

                    _db.ResellerFeatures.RemoveRange(oldFeatures);

                    foreach (var f in dto.Features)
                    {
                        _db.ResellerFeatures.Add(new ResellerFeature
                        {
                            ResellerId = resellerId,
                            FeatureId = f.FeatureId,
                            IsEnabled = f.IsEnabled,
                            GrantedByAdminId = GetCurrentAdminId(),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                reseller.UpdatedAt = DateTime.UtcNow;
                _db.Resellers.Update(reseller);
                await _db.SaveChangesAsync();

                return Ok(new ApiResponse(true, "Reseller updated")
                {
                    ErrorCode = null
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        [Authorize(Roles = "super_admin")]
        [HttpDelete("resellers/{resellerId:guid}")]
        public async Task<ActionResult<ApiResponse>> DeleteReseller(Guid resellerId)
        {
            if (!await _permissionService.CanPerformAsync(User, "resellers.delete"))
                return Unauthorized(new ApiResponse(false, "No permission to delete reseller") { ErrorCode = "OC-117" });

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

        [Authorize(Roles = "super_admin")]
        [HttpPatch("tenants/{tenantId:guid}/reseller")]
        public async Task<ActionResult<ApiResponse>> MoveTenantToReseller(Guid tenantId, [FromBody] MoveTenantDto dto)
        {
            if (!await _permissionService.CanPerformAsync(User, "tenants.move"))
                return Unauthorized(new ApiResponse(false, "No permission to move tenants") { ErrorCode = "OC-118" });

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
            if (!await _permissionService.CanPerformAsync(User, "tenants.view"))
                return Unauthorized(new ApiResponse(false, "No permission to view all tenants") { ErrorCode = "OC-119" });

            var tenants = await _db.Tenants
                .Include(t => t.AssignedReseller)
                .Select(t => new
                {
                    t.TenantId,
                    t.DateCreated,
                    t.DateUpdated,
                    t.IsActive,
                    CurrentResellerName = t.AssignedReseller != null ? t.AssignedReseller.Name : "Unassigned"
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
            if (!await _permissionService.CanPerformAsync(User, "resellers.view"))
                return Unauthorized(new ApiResponse(false, "No permission to list resellers for move") { ErrorCode = "OC-120" });

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
}