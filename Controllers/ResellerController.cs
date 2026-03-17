using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.DTO;
using OcufiiAPI.Enums;
using OcufiiAPI.Models;
using OcufiiAPI.Services;
using OcufiiAPI.Validators;
using System.Security.Claims;

namespace OcufiiAPI.Controllers
{
    [ApiController]
    [Route("reseller")]
    [Authorize(Roles = "reseller_admin")]
    [Produces("application/json")]
    public class ResellerController : ControllerBase
    {
        private readonly OcufiiDbContext _db;
        private readonly PermissionService _permissionService;

        public ResellerController(OcufiiDbContext db, PermissionService permissionService)
        {
            _db = db;
            _permissionService = permissionService;
        }

        private Guid GetResellerId() => Guid.Parse(User.FindFirst("reseller_id")?.Value!);

        [HttpGet("my-tenants")]
        public async Task<ActionResult<ApiResponse>> ListMyTenants()
        {
            if (!await _permissionService.CanPerformAsync(User, "tenants.view"))
                return Unauthorized(new ApiResponse(false, "No permission to view tenants") { ErrorCode = "OC-121" });

            var resellerId = GetResellerId();
            var tenants = await _db.Tenants
                .Where(t => t.AssignedResellerId == resellerId)
                .Select(t => new
                {
                    tenantId = t.TenantId,
                    t.DateCreated,
                    t.DateUpdated,
                    t.IsActive,
                    Status = t.IsActive ? "Active" : "Inactive",
                    Permissions = _db.TenantPermissions
                        .Where(tp => tp.TenantId == t.TenantId)
                        .Join(_db.Permissions, tp => tp.PermissionId, p => p.PermissionId, (tp, p) => new { p.Key, tp.IsGranted })
                        .ToList(),
                    Features = _db.TenantFeatures
                        .Where(tf => tf.TenantId == t.TenantId)
                        .Join(_db.Features, tf => tf.FeatureId, f => f.Id, (tf, f) => new { f.Key, tf.IsEnabled })
                        .ToList()
                })
                .ToListAsync();

            return Ok(new ApiResponse(true, "My tenants retrieved")
            {
                Data = tenants,
                ErrorCode = null
            });
        }

        [HttpGet("my-features-permissions")]
        public async Task<ActionResult<ApiResponse>> GetMyFeaturesAndPermissions()
        {
            var resellerId = GetResellerId();

            // Features grouped by FeatureType
            var featuresGrouped = await _db.ResellerFeatures
                .Where(rf => rf.ResellerId == resellerId)
                .Join(_db.Features,
                      rf => rf.FeatureId,
                      f => f.Id,
                      (rf, f) => new
                      {
                          f.Id,
                          f.Key,
                          f.Name,
                          f.Description,
                          f.FeatureType,
                          rf.IsEnabled,
                          rf.CreatedAt,
                          rf.UpdatedAt
                      })
                .GroupBy(f => f.FeatureType)
                .Select(g => new
                {
                    FeatureType = g.Key.ToString(),
                    Items = g.Select(f => new
                    {
                        f.Id,
                        f.Key,
                        f.Name,
                        f.Description,
                        f.IsEnabled,
                        f.CreatedAt,
                        f.UpdatedAt
                    }).OrderBy(f => f.Key).ToList()
                })
                .OrderBy(g => g.FeatureType)
                .ToListAsync();

            // Permissions grouped by Category
            var permissionsGrouped = await _db.ResellerPermissions
                .Where(rp => rp.ResellerId == resellerId)
                .Join(_db.Permissions,
                      rp => rp.PermissionId,
                      p => p.PermissionId,
                      (rp, p) => new
                      {
                          p.PermissionId,
                          p.Key,
                          p.Name,
                          p.Description,
                          p.Category,
                          p.IsDefault,
                          rp.IsGranted,
                          rp.CreatedAt,
                          rp.UpdatedAt
                      })
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
                        p.IsDefault,
                        p.IsGranted,
                        p.CreatedAt,
                        p.UpdatedAt
                    }).OrderBy(p => p.Key).ToList()
                })
                .OrderBy(g => g.Category)
                .ToListAsync();

            return Ok(new ApiResponse(true, "Reseller's assigned features and permissions retrieved")
            {
                Data = new
                {
                    FeaturesByType = featuresGrouped,
                    PermissionsByCategory = permissionsGrouped
                },
                ErrorCode = null
            });
        }

        [HttpPost("my-tenants")]
        public async Task<ActionResult<ApiResponse>> CreateTenantAsReseller([FromBody] CreateTenantDto dto)
        {
            if (!await _permissionService.CanPerformAsync(User, "tenants.create"))
                return Unauthorized(new ApiResponse(false, "No permission to create tenants") { ErrorCode = "OC-122" });

            var validator = new TenantUserValidator();
            var validationResult = await validator.ValidateAsync(dto);
            if (!validationResult.IsValid)
                return BadRequest(new ApiResponse(false, "Validation failed")
                {
                    Data = new { errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray() },
                    ErrorCode = "OC-003"
                });

            if (string.IsNullOrWhiteSpace(dto.OwnerEmail))
                return BadRequest(new ApiResponse(false, "Email is required") { ErrorCode = "OC-080" });

            dto.OwnerEmail = dto.OwnerEmail.Trim().ToLowerInvariant();

            var userExists = await _db.Users
                .AnyAsync(u => u.Email == dto.OwnerEmail && !u.IsDeleted && u.DeletedAt == null);

            if (userExists)
                return Conflict(new ApiResponse(false, "Email already in use") { ErrorCode = "OC-081" });

            var resellerId = GetResellerId();

            var tenant = new Tenant
            {
                TenantId = Guid.NewGuid(),
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
            var hash = new PasswordHasher<User>().HashPassword(null!, tempPassword);

            var owner = new User
            {
                UserId = Guid.NewGuid(),
                Email = dto.OwnerEmail,
                FirstName = dto.OwnerFirstName,
                LastName = dto.OwnerLastName ?? "",
                PhoneNumber = dto.PhoneNumber,
                Password = hash,
                RoleId = (await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "account_owner"))!.RoleId,
                TenantId = tenant.TenantId,
                IsEnabled = true,
                IsDeleted = false,
                DateSubmitted = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow
            };

            _db.Users.Add(owner);
            await _db.SaveChangesAsync();

            try
            {
                if (dto.Permissions != null && dto.Permissions.Any())
                {
                    var requestedAccountPermIds = dto.Permissions
                        .Where(p => _db.Permissions.Any(x => x.PermissionId == p.PermissionId && x.Category == "account"))
                        .Select(p => p.PermissionId)
                        .Distinct()
                        .ToList();

                    var resellerHasPermIds = await _db.ResellerPermissions
                        .Where(rp => rp.ResellerId == resellerId && rp.IsGranted)
                        .Select(rp => rp.PermissionId)
                        .ToListAsync();

                    var invalidPermIds = requestedAccountPermIds
                        .Where(id => !resellerHasPermIds.Contains(id))
                        .ToList();

                    if (invalidPermIds.Any())
                    {
                        _db.Tenants.Remove(tenant);
                        _db.Users.Remove(owner);
                        await _db.SaveChangesAsync();
                        return BadRequest(new ApiResponse(false, "Reseller can only assign account permissions that were granted to him") { ErrorCode = "OC-133" });
                    }

                    foreach (var p in dto.Permissions)
                    {
                        _db.TenantPermissions.Add(new TenantPermission
                        {
                            TenantId = tenant.TenantId,
                            PermissionId = p.PermissionId,
                            IsGranted = p.IsGranted,
                            GrantedByResellerId = resellerId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                if (dto.Features != null && dto.Features.Any())
                {
                    var requestedFeatureIds = dto.Features.Select(f => f.FeatureId).Distinct().ToList();

                    var resellerHasFeatureIds = await _db.ResellerFeatures
                        .Where(rf => rf.ResellerId == resellerId && rf.IsEnabled)
                        .Select(rf => rf.FeatureId)
                        .ToListAsync();

                    var invalidFeatureIds = requestedFeatureIds
                        .Where(id => !resellerHasFeatureIds.Contains(id))
                        .ToList();

                    if (invalidFeatureIds.Any())
                    {
                        _db.Tenants.Remove(tenant);
                        _db.Users.Remove(owner);
                        await _db.SaveChangesAsync();
                        return BadRequest(new ApiResponse(false, "Reseller can only assign features that were enabled for him") { ErrorCode = "OC-134" });
                    }

                    foreach (var f in dto.Features)
                    {
                        _db.TenantFeatures.Add(new TenantFeature
                        {
                            TenantId = tenant.TenantId,
                            FeatureId = f.FeatureId,
                            IsEnabled = f.IsEnabled,
                            GrantedByResellerId = resellerId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _db.SaveChangesAsync();

                return Created($"/reseller/my-tenants/{tenant.TenantId}", new ApiResponse(true, "Tenant and account owner created")
                {
                    Data = new { TenantId = tenant.TenantId, OwnerUserId = owner.UserId, TemporaryPassword = tempPassword },
                    ErrorCode = null
                });
            }
            catch (Exception)
            {
                _db.Tenants.Remove(tenant);
                _db.Users.Remove(owner);
                await _db.SaveChangesAsync();
                throw;
            }
        }

        [HttpPatch("my-tenants/{tenantId:guid}")]
        public async Task<ActionResult<ApiResponse>> UpdateTenant(Guid tenantId, [FromBody] UpdateTenantDto dto)
        {
            if (!await _permissionService.CanPerformAsync(User, "tenants.edit"))
                return Unauthorized(new ApiResponse(false, "No permission to update tenants") { ErrorCode = "OC-124" });

            var resellerId = GetResellerId();
            var tenant = await _db.Tenants
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.AssignedResellerId == resellerId);

            if (tenant == null)
                return NotFound(new ApiResponse(false, "Tenant not found or not assigned to this reseller") { ErrorCode = "OC-062" });

            try
            {
                // Update basic info
                if (dto.Name != null)
                {
                    var owner = await _db.Users
                        .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Role.RoleName == "account_owner");

                    if (owner != null)
                    {
                        var names = dto.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        owner.FirstName = names.Length > 0 ? names[0] : owner.FirstName;
                        owner.LastName = names.Length > 1 ? string.Join(" ", names.Skip(1)) : owner.LastName;
                        owner.DateUpdated = DateTime.UtcNow;
                        _db.Users.Update(owner);
                    }
                }

                // Validate & Update Tenant Permissions (only account category + reseller must have them)
                if (dto.Permissions != null)
                {
                    var requestedAccountPermIds = dto.Permissions
                        .Where(p => _db.Permissions.Any(x => x.PermissionId == p.PermissionId && x.Category == "account"))
                        .Select(p => p.PermissionId)
                        .Distinct()
                        .ToList();

                    // Get permissions the reseller actually has
                    var resellerHasPermIds = await _db.ResellerPermissions
                        .Where(rp => rp.ResellerId == resellerId && rp.IsGranted)
                        .Select(rp => rp.PermissionId)
                        .ToListAsync();

                    var invalidPermIds = requestedAccountPermIds
                        .Where(id => !resellerHasPermIds.Contains(id))
                        .ToList();

                    if (invalidPermIds.Any())
                    {
                        return BadRequest(new ApiResponse(false, "Reseller can only assign account permissions that were granted to him") { ErrorCode = "OC-133" });
                    }

                    // Remove old permissions
                    var oldPermissions = await _db.TenantPermissions
                        .Where(tp => tp.TenantId == tenantId)
                        .ToListAsync();

                    _db.TenantPermissions.RemoveRange(oldPermissions);

                    // Add new/updated permissions
                    foreach (var p in dto.Permissions)
                    {
                        _db.TenantPermissions.Add(new TenantPermission
                        {
                            TenantId = tenantId,
                            PermissionId = p.PermissionId,
                            IsGranted = p.IsGranted,
                            GrantedByResellerId = resellerId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Validate & Update Tenant Features (only those reseller has)
                if (dto.Features != null)
                {
                    var requestedFeatureIds = dto.Features.Select(f => f.FeatureId).Distinct().ToList();

                    var resellerHasFeatureIds = await _db.ResellerFeatures
                        .Where(rf => rf.ResellerId == resellerId && rf.IsEnabled)
                        .Select(rf => rf.FeatureId)
                        .ToListAsync();

                    var invalidFeatureIds = requestedFeatureIds
                        .Where(id => !resellerHasFeatureIds.Contains(id))
                        .ToList();

                    if (invalidFeatureIds.Any())
                    {
                        return BadRequest(new ApiResponse(false, "Reseller can only assign features that were enabled for him") { ErrorCode = "OC-134" });
                    }

                    // Remove old features
                    var oldFeatures = await _db.TenantFeatures
                        .Where(tf => tf.TenantId == tenantId)
                        .ToListAsync();

                    _db.TenantFeatures.RemoveRange(oldFeatures);

                    // Add new/updated features
                    foreach (var f in dto.Features)
                    {
                        _db.TenantFeatures.Add(new TenantFeature
                        {
                            TenantId = tenantId,
                            FeatureId = f.FeatureId,
                            IsEnabled = f.IsEnabled,
                            GrantedByResellerId = resellerId,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                tenant.DateUpdated = DateTime.UtcNow;
                _db.Tenants.Update(tenant);
                await _db.SaveChangesAsync();

                return Ok(new ApiResponse(true, "Tenant updated")
                {
                    ErrorCode = null
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPatch("my-tenants/{tenantId:guid}/status")]
        public async Task<ActionResult<ApiResponse>> UpdateTenantStatus(Guid tenantId, [FromBody] AdminUpdateStatusDto dto)
        {
            if (!await _permissionService.CanPerformAsync(User, "tenants.edit"))
                return Unauthorized(new ApiResponse(false, "No permission to update tenant status") { ErrorCode = "OC-125" });

            var resellerId = GetResellerId();
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.AssignedResellerId == resellerId);
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

        [HttpDelete("my-tenants/{tenantId:guid}")]
        public async Task<ActionResult<ApiResponse>> DeleteTenant(Guid tenantId)
        {
            if (!await _permissionService.CanPerformAsync(User, "tenants.delete"))
                return Unauthorized(new ApiResponse(false, "No permission to deactivate tenant") { ErrorCode = "OC-126" });

            var resellerId = GetResellerId();
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.AssignedResellerId == resellerId);
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
}