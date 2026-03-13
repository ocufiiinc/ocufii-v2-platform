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

namespace OcufiiAPI.Controllers;

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
        var resellerId = GetResellerId();
        var tenants = await _db.Tenants
            .Where(t => t.AssignedResellerId == resellerId)
            .Select(t => new
            {
                tenantId = t.ResellerId,
                t.DateCreated,
                t.DateUpdated,
                t.IsActive,
                Status = t.IsActive ? "Active" : "Inactive",
                AssignedFeatures = _db.UserFeatures
                    .Where(uf => uf.User.TenantId == t.ResellerId && uf.User.Role.RoleName == "account_owner")
                    .Join(_db.Features, uf => uf.FeatureId, f => f.Id, (uf, f) => new
                    {
                        f.Id,
                        f.Key,
                        f.Name,
                        uf.IsEnabled,
                        Rights = new
                        {
                            OnlyView = uf.OnlyView,
                            CanEdit = uf.CanEdit,
                            FullAccess = uf.FullAccess,
                            CanCreate = uf.CanCreate,
                            CanDelete = uf.CanDelete
                        }
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "My tenants retrieved")
        {
            Data = tenants,
            ErrorCode = null
        });
    }
    [HttpPost("my-tenants")]
    public async Task<ActionResult<ApiResponse>> CreateTenantAsReseller([FromBody] CreateTenantDto dto)
    {
        var validator = new TenantUserValidator();
        var validationResult = await validator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiResponse(false, "Validation failed")
            {
                Data = new { errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray() },
                ErrorCode = "OC-003"
            });
        }

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
            TenantId = tenant.ResellerId,
            IsEnabled = true,
            IsDeleted = false,
            DateSubmitted = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow
        };

        _db.Users.Add(owner);
        await _db.SaveChangesAsync();

        try
        {
            var allowedFeatureIds = await _db.ResellerAllowedTenantFeatures
                .Where(raf => raf.ResellerId == resellerId && raf.IsEnabled)
                .Select(raf => raf.FeatureId)
                .ToListAsync();

            var requestedFeatures = dto.TenantFeatures ?? new List<FeatureAssignmentDto>();

            if (!requestedFeatures.Any())
            {
                requestedFeatures = allowedFeatureIds
                    .Select(id => new FeatureAssignmentDto { FeatureId = id, IsEnabled = true })
                    .ToList();
            }
            else
            {
                var invalidAllowed = requestedFeatures
                    .Where(f => !allowedFeatureIds.Contains(f.FeatureId))
                    .ToList();

                if (invalidAllowed.Any())
                {
                    _db.Tenants.Remove(tenant);
                    _db.Users.Remove(owner);
                    await _db.SaveChangesAsync();

                    return BadRequest(new ApiResponse(false, "Requested features not allowed by platform") { ErrorCode = "OC-092" });
                }

                var validTenantFeatureIds = await _db.Features
                    .Where(f => f.FeatureType == FeatureType.Tenant)
                    .Select(f => f.Id)
                    .ToListAsync();

                var invalidType = requestedFeatures
                    .Where(f => !validTenantFeatureIds.Contains(f.FeatureId))
                    .ToList();

                if (invalidType.Any())
                {
                    _db.Tenants.Remove(tenant);
                    _db.Users.Remove(owner);
                    await _db.SaveChangesAsync();

                    return BadRequest(new ApiResponse(false,
                        $"Invalid feature IDs (must be tenant-type): {string.Join(", ", invalidType.Select(x => x.FeatureId))}")
                    { ErrorCode = "OC-094" });
                }
            }

            foreach (var assignment in requestedFeatures)
            {
                _db.UserFeatures.Add(new UserFeature
                {
                    UserId = owner.UserId,
                    FeatureId = assignment.FeatureId,
                    IsEnabled = assignment.IsEnabled,
                    OnlyView = true,
                    CanEdit = true,
                    FullAccess = true,
                    CanCreate = true,
                    CanDelete = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();

            // TODO: Send email with temp password

            return Created($"/reseller/my-tenants/{tenant.ResellerId}", new ApiResponse(true, "Tenant and account owner created")
            {
                Data = new { TenantId = tenant.ResellerId, OwnerUserId = owner.UserId, TemporaryPassword = tempPassword },
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
        var resellerId = GetResellerId();
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

        if (dto.TenantFeatures != null)
        {
            var allowedFeatureIds = await _db.ResellerAllowedTenantFeatures
                .Where(raf => raf.ResellerId == resellerId && raf.IsEnabled)
                .Select(raf => raf.FeatureId)
                .ToListAsync();

            var invalidAllowed = dto.TenantFeatures.Where(f => !allowedFeatureIds.Contains(f.FeatureId)).ToList();
            if (invalidAllowed.Any())
                return BadRequest(new ApiResponse(false, "Requested features not allowed by platform") { ErrorCode = "OC-092" });

            var validTenantFeatureIds = await _db.Features
                .Where(f => f.FeatureType == FeatureType.Tenant)
                .Select(f => f.Id)
                .ToListAsync();

            var invalidType = dto.TenantFeatures.Where(f => !validTenantFeatureIds.Contains(f.FeatureId)).ToList();
            if (invalidType.Any())
                return BadRequest(new ApiResponse(false, $"Invalid feature IDs (must be tenant-type): {string.Join(", ", invalidType.Select(x => x.FeatureId))}") { ErrorCode = "OC-094" });

            var oldFeatures = await _db.UserFeatures.Where(uf => uf.UserId == owner.UserId).ToListAsync();
            _db.UserFeatures.RemoveRange(oldFeatures);

            foreach (var assignment in dto.TenantFeatures)
            {
                _db.UserFeatures.Add(new UserFeature
                {
                    UserId = owner.UserId,
                    FeatureId = assignment.FeatureId,
                    IsEnabled = assignment.IsEnabled,
                    OnlyView = assignment.OnlyView,
                    CanEdit = assignment.CanEdit,
                    FullAccess = assignment.FullAccess,
                    CanCreate = assignment.CanCreate,
                    CanDelete = assignment.CanDelete,
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

    [HttpPatch("my-tenants/{tenantId:guid}/status")]
    public async Task<ActionResult<ApiResponse>> UpdateTenantStatus(Guid tenantId, [FromBody] AdminUpdateStatusDto dto)
    {
        var resellerId = GetResellerId();
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

    [HttpDelete("my-tenants/{tenantId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteTenant(Guid tenantId)
    {
        var resellerId = GetResellerId();
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