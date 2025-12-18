using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.DTO;
using OcufiiAPI.Models;
using System.Security.Claims;

namespace OcufiiAPI.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Roles = "super_admin,tenant_admin")]
public class AdminController : ControllerBase
{
    private readonly OcufiiDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();
    public AdminController(OcufiiDbContext db)
    {
        _db = db;
    }

    // GET /admin/tenants — Super Admin only
    [HttpGet("tenants")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse>> ListTenants()
    {
        var tenants = await _db.Tenants
            .Include(t => t.Users)
                .ThenInclude(u => u.Role)
            .Select(t => new
            {
                t.ResellerId,
                t.DateCreated,
                t.DateUpdated,
                t.ThemeConfig,
                t.CustomWorkflows,
                userCount = t.Users.Count,
                adminCount = t.Users.Count(u => u.Role.RoleName == "tenant_admin" || u.Role.RoleName == "super_admin"),
                users = t.Users.Select(u => new
                {
                    u.UserId,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.PhoneNumber,
                    u.Company,
                    u.IsEnabled,
                    role = u.Role.RoleName,
                    u.AccountType,
                    u.ParentId
                }).ToList()
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Tenants retrieved successfully")
        {
            Data = tenants
        });
    }

    // POST /admin/tenants — Super Admin only
    [HttpPost("tenants")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse>> CreateTenant([FromBody] CreateTenantRequest request)
    {
        var tenant = new Tenant
        {
            ResellerId = Guid.NewGuid(),
            DateCreated = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            ThemeConfig = request.ThemeConfig ?? "{}",
            CustomWorkflows = request.CustomWorkflows ?? "[]"
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        return Created($"/admin/tenants/{tenant.ResellerId}", new ApiResponse(true, "Tenant created successfully")
        {
            Data = tenant
        });
    }

    // GET /admin/users — Tenant Admins see their tenant, Super Admin sees all
    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse>> ListUsers()
    {
        var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId);

        if (currentUser == null)
            return NotFound(new ApiResponse(false, "Current user not found"));

        var isSuperAdmin = User.IsInRole("super_admin");

        var query = _db.Users.Where(u => !u.IsDeleted);

        if (!isSuperAdmin)
            query = query.Where(u => u.TenantId == currentUser.TenantId);

        var users = await query
            .Select(u => new
            {
                u.UserId,
                u.Email,
                u.FirstName,
                u.LastName,
                u.PhoneNumber,
                u.Company,
                u.IsEnabled,
                u.TenantId,
                u.RoleId,
                ParentId = u.ParentId ?? u.UserId
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Users retrieved successfully")
        {
            Data = users
        });
    }

    // POST /admin/users — Create top-level user
    [HttpPost("users")]
    public async Task<ActionResult<ApiResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var currentUser = await _db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.UserId == currentUserId);

        if (currentUser == null)
            return NotFound(new ApiResponse(false, "Current user not found"));

        var isSuperAdmin = User.IsInRole("super_admin");

        Guid tenantId;
        if (isSuperAdmin)
        {
            if (request.TenantId == null)
                return BadRequest(new ApiResponse(false, "TenantId required for super admin"));
            tenantId = request.TenantId.Value;
        }
        else
        {
            tenantId = currentUser.TenantId!.Value;
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == request.Role);
        if (role == null)
            return BadRequest(new ApiResponse(false, "Invalid role"));

        var newUser = new User
        {
            UserId = Guid.NewGuid(),
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Company = request.Company,
            Password = _hasher.HashPassword(null!, "TempPass@2025!"),
            RoleId = role.RoleId,
            TenantId = tenantId,
            ParentId = currentUser.UserId, // Top-level admin creates
            IsEnabled = true,
            IsDeleted = false,
            DateSubmitted = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync();

        return Created($"/admin/users/{newUser.UserId}", new ApiResponse(true, "User created successfully")
        {
            Data = new { newUser.UserId, newUser.Email }
        });
    }

    // POST /admin/users/{id}/dependents — Create dependent user with features
    [HttpPost("users/{id:guid}/dependents")]
    public async Task<ActionResult<ApiResponse>> CreateDependent(Guid id, [FromBody] CreateDependentRequest request)
    {
        var parent = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id && !u.IsDeleted);
        if (parent == null)
            return NotFound(new ApiResponse(false, "Parent user not found"));

        var roleName = string.IsNullOrEmpty(request.Role) ? "viewer" : request.Role;
        var userRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == roleName);
        if (userRole == null)
            return BadRequest(new ApiResponse(false, $"Role '{roleName}' not found"));

        var dependent = new User
        {
            UserId = Guid.NewGuid(),
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Company = request.Company ?? parent.Company,
            Username = request.Email.Split('@')[0],
            Password = _hasher.HashPassword(null!, "TempPass@2025!"), // Change later
            RoleId = userRole.RoleId,
            TenantId = parent.TenantId,
            ParentId = parent.UserId,
            IsEnabled = true,
            IsDeleted = false,
            DateSubmitted = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow
        };

        _db.Users.Add(dependent);
        await _db.SaveChangesAsync();

        // Assign features if provided
        var assignedFeatures = new List<UserFeature>();
        if (request.Features != null)
        {
            foreach (var f in request.Features)
            {
                var feature = await _db.Features.FirstOrDefaultAsync(x => x.Id == f.FeatureId);
                if (feature == null) continue;  // Skip invalid

                var userFeature = new UserFeature
                {
                    UserId = dependent.UserId,
                    FeatureId = f.FeatureId,
                    IsEnabled = f.IsEnabled,
                    Right = f.Right,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.UserFeatures.Add(userFeature);
                assignedFeatures.Add(userFeature);
            }
            await _db.SaveChangesAsync();
        }

        return Created($"/admin/users/{dependent.UserId}", new ApiResponse(true, "Dependent user created successfully")
        {
            Data = new
            {
                dependent.UserId,
                dependent.Email,
                dependent.FirstName,
                dependent.LastName,
                role = roleName,
                parentId = parent.UserId,
                assignedFeatures = assignedFeatures.Select(uf => new { uf.FeatureId, uf.IsEnabled, uf.Right })
            }
        });
    }

    // GET /admin/users/{id}/features — Get features for dependent
    [HttpGet("users/{id:guid}/features")]
    public async Task<ActionResult<ApiResponse>> GetUserFeatures(Guid id)
    {
        var user = await _db.Users
            .Include(u => u.UserFeatures)
            .ThenInclude(uf => uf.Feature)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound(new ApiResponse(false, "User not found"));

        if (user.ParentId == user.UserId || user.ParentId == null)
            return BadRequest(new ApiResponse(false, "User is not a dependent"));

        return Ok(new ApiResponse(true, "User features retrieved")
        {
            Data = user.UserFeatures.Select(uf => new
            {
                uf.FeatureId,
                uf.IsEnabled,
                uf.Right,
                uf.Feature.Key,
                uf.Feature.Name
            })
        });
    }

    // GET /admin/features — List all features (for getting IDs)
    [HttpGet("features")]
    public async Task<ActionResult<ApiResponse>> ListFeatures()
    {
        var features = await _db.Features.ToListAsync();

        return Ok(new ApiResponse(true, "Features retrieved successfully")
        {
            Data = features.Select(f => new
            {
                f.Id,
                f.Key,
                f.Name,
                f.Description,
                f.DeviceTypeId
            })
        });
    }

    // POST /admin/users/{id}/features — Assign feature to dependent
    [HttpPost("users/{id:guid}/features")]
    public async Task<ActionResult<ApiResponse>> AssignFeature(Guid id, [FromBody] AssignFeatureRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id && !u.IsDeleted);
        if (user == null)
            return NotFound(new ApiResponse(false, "User not found"));

        if (user.ParentId == user.UserId || user.ParentId == null)
            return BadRequest(new ApiResponse(false, "User is not a dependent"));

        var feature = await _db.Features.FirstOrDefaultAsync(f => f.Id == request.FeatureId);
        if (feature == null)
            return BadRequest(new ApiResponse(false, "Invalid feature"));

        var existing = await _db.UserFeatures.FirstOrDefaultAsync(uf => uf.UserId == id && uf.FeatureId == request.FeatureId);
        if (existing != null)
            return Conflict(new ApiResponse(false, "Feature already assigned"));

        var userFeature = new UserFeature
        {
            UserId = user.UserId,
            FeatureId = feature.Id,
            IsEnabled = request.IsEnabled,
            Right = request.Right,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.UserFeatures.Add(userFeature);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Feature assigned successfully")
        {
            Data = new
            {
                userFeature.FeatureId,
                userFeature.IsEnabled,
                userFeature.Right
            }
        });
    }

    // PATCH /admin/users/{id}/features/{featureId} — Update feature
    [HttpPatch("users/{id:guid}/features/{featureId:guid}")]
    public async Task<ActionResult<ApiResponse>> UpdateFeature(Guid id, Guid featureId, [FromBody] UpdateFeatureRequest request)
    {
        var userFeature = await _db.UserFeatures.FirstOrDefaultAsync(uf => uf.UserId == id && uf.FeatureId == featureId);
        if (userFeature == null)
            return NotFound(new ApiResponse(false, "User feature not found"));

        userFeature.IsEnabled = request.IsEnabled;
        userFeature.Right = request.Right;
        userFeature.UpdatedAt = DateTime.UtcNow;

        _db.UserFeatures.Update(userFeature);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Feature updated successfully")
        {
            Data = new
            {
                userFeature.FeatureId,
                userFeature.IsEnabled,
                userFeature.Right
            }
        });
    }

    // DELETE /admin/users/{id}/features/{featureId} — Remove feature
    [HttpDelete("users/{id:guid}/features/{featureId:guid}")]
    public async Task<ActionResult<ApiResponse>> RemoveFeature(Guid id, Guid featureId)
    {
        var userFeature = await _db.UserFeatures.FirstOrDefaultAsync(uf => uf.UserId == id && uf.FeatureId == featureId);
        if (userFeature == null)
            return NotFound(new ApiResponse(false, "User feature not found"));

        _db.UserFeatures.Remove(userFeature);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Feature removed successfully"));
    }
}

// NEW DTO for CreateDependent with features
public class CreateDependentRequest : CreateUserRequest
{
    public List<AssignFeatureRequest>? Features { get; set; }
}