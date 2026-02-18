using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.DTO;
using OcufiiAPI.Models;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace OcufiiAPI.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Roles = "super_admin,tenant_admin")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse), 200)]
[ProducesResponseType(typeof(ApiResponse), 400)]
[ProducesResponseType(typeof(ApiResponse), 401)]
[ProducesResponseType(typeof(ApiResponse), 403)]
[ProducesResponseType(typeof(ApiResponse), 404)]
[ProducesResponseType(typeof(ApiResponse), 409)]
[ProducesResponseType(typeof(ApiResponse), 500)]
public class AdminController : ControllerBase
{
    private readonly OcufiiDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();

    public AdminController(OcufiiDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// List all tenants with user and admin counts (Super Admin only)
    /// </summary>
    /// <remarks>
    /// Returns all tenants including basic user stats. Restricted to super_admin role.
    /// </remarks>
    /// <response code="200">Tenants retrieved successfully</response>
    /// <response code="401">Unauthorized - not authenticated</response>
    /// <response code="403">Forbidden - not super_admin</response>
    [HttpGet("tenants")]
    [Authorize(Roles = "super_admin")]
    [SwaggerOperation(Summary = "List All Tenants", Description = "Retrieves all tenants with user and admin counts. Super admin only.")]
    [SwaggerResponse(200, "Tenants retrieved", typeof(ApiResponse))]
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

    /// <summary>
    /// Create a new tenant (Super Admin only)
    /// </summary>
    /// <remarks>
    /// Creates a new tenant record. Restricted to super_admin role.
    /// </remarks>
    /// <param name="request">Tenant creation details</param>
    /// <response code="201">Tenant created successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden - not super_admin</response>
    [HttpPost("tenants")]
    [Authorize(Roles = "super_admin")]
    [SwaggerOperation(Summary = "Create New Tenant", Description = "Creates a new tenant. Super admin only.")]
    [SwaggerResponse(201, "Tenant created", typeof(ApiResponse))]
    public async Task<ActionResult<ApiResponse>> CreateTenant([FromBody] CreateTenantRequest request)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body"));

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
            Data = new { tenant.ResellerId, tenant.DateCreated }
        });
    }

    /// <summary>
    /// List users (Super Admin sees all, Tenant Admin sees only their tenant)
    /// </summary>
    /// <remarks>
    /// Returns user list filtered by tenant for tenant admins. Super admins see everything.
    /// </remarks>
    /// <response code="200">Users retrieved successfully</response>
    /// <response code="401">Unauthorized</response>
    [HttpGet("users")]
    [SwaggerOperation(Summary = "List Users", Description = "Lists users - full access for super admin, tenant-scoped for tenant admin")]
    [SwaggerResponse(200, "Users retrieved", typeof(ApiResponse))]
    public async Task<ActionResult<ApiResponse>> ListUsers()
    {
        var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId);

        if (currentUser == null)
            return NotFound(new ApiResponse(false, "Current user not found"));

        var isSuperAdmin = User.IsInRole("super_admin");

        var query = _db.Users
            .Where(u => !u.IsDeleted);

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

    /// <summary>
    /// Create a new user (Super Admin or Tenant Admin)
    /// </summary>
    /// <remarks>
    /// Creates a user in the current tenant (or any tenant for super admin).
    /// </remarks>
    /// <param name="request">User creation details</param>
    /// <response code="201">User created</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden</response>
    [HttpPost("users")]
    [SwaggerOperation(Summary = "Create User", Description = "Creates a new user in the tenant")]
    [SwaggerResponse(201, "User created", typeof(ApiResponse))]
    [SwaggerResponse(400, "Invalid request")]
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
            ParentId = currentUser.UserId,
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

    /// <summary>
    /// Create a dependent user (Super Admin or Tenant Admin)
    /// </summary>
    /// <remarks>
    /// Creates a dependent user linked to a parent user.
    /// </remarks>
    /// <param name="request">Dependent user details</param>
    /// <response code="201">Dependent created</response>
    /// <response code="400">Invalid request</response>
    [HttpPost("users/{id:guid}/dependents")]
    [SwaggerOperation(Summary = "Create Dependent User", Description = "Creates a dependent user for the specified parent")]
    [SwaggerResponse(201, "Dependent created")]
    [SwaggerResponse(400, "Invalid request")]
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
            Password = _hasher.HashPassword(null!, "TempPass@2025!"),
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

        return Created($"/admin/users/{dependent.UserId}", new ApiResponse(true, "Dependent created successfully")
        {
            Data = new { dependent.UserId, dependent.Email }
        });
    }

    /// <summary>
    /// Get features assigned to a user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <response code="200">Features retrieved</response>
    /// <response code="404">User not found</response>
    [HttpGet("users/{id:guid}/features")]
    [SwaggerOperation(Summary = "Get User Features", Description = "Returns all features assigned to the user")]
    [SwaggerResponse(200, "Features retrieved")]
    [SwaggerResponse(404, "User not found")]
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

    /// <summary>
    /// Assign a feature to a user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="request">Feature assignment details</param>
    /// <response code="200">Feature assigned</response>
    /// <response code="400">Invalid request</response>
    /// <response code="404">User or feature not found</response>
    [HttpPost("users/{id:guid}/features")]
    [SwaggerOperation(Summary = "Assign Feature to User", Description = "Assigns a feature to a user with enabled/right settings")]
    [SwaggerResponse(200, "Feature assigned")]
    [SwaggerResponse(400, "Invalid request")]
    [SwaggerResponse(404, "User or feature not found")]
    public async Task<ActionResult<ApiResponse>> AssignFeature(Guid id, [FromBody] AssignFeatureRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id && !u.IsDeleted);
        if (user == null)
            return NotFound(new ApiResponse(false, "User not found"));

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

    /// <summary>
    /// Update a feature assignment for a user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="featureId">Feature ID</param>
    /// <param name="request">Update details</param>
    /// <response code="200">Feature updated</response>
    /// <response code="404">User feature not found</response>
    [HttpPatch("users/{id:guid}/features/{featureId:guid}")]
    [SwaggerOperation(Summary = "Update User Feature", Description = "Updates enabled/right settings for a feature assigned to user")]
    [SwaggerResponse(200, "Feature updated")]
    [SwaggerResponse(404, "User feature not found")]
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

    /// <summary>
    /// Remove a feature assignment from a user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="featureId">Feature ID</param>
    /// <response code="200">Feature removed</response>
    /// <response code="404">User feature not found</response>
    [HttpDelete("users/{id:guid}/features/{featureId:guid}")]
    [SwaggerOperation(Summary = "Remove User Feature", Description = "Removes a feature assignment from the user")]
    [SwaggerResponse(200, "Feature removed")]
    [SwaggerResponse(404, "User feature not found")]
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

public class CreateDependentRequest : CreateUserRequest
{
    public List<AssignFeatureRequest>? Features { get; set; }
}