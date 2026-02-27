using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.DTO;
using OcufiiAPI.Enums;
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
                t.AssignedResellerId,
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
            Data = tenants,
            ErrorCode = null
        });
    }

    [HttpPost("tenants")]
    [Authorize(Roles = "super_admin")]
    [SwaggerOperation(Summary = "Create New Tenant", Description = "Creates a new tenant. Super admin only. AssignedResellerId defaults to Ocufii Direct if not provided.")]
    [SwaggerResponse(201, "Tenant created", typeof(ApiResponse))]
    public async Task<ActionResult<ApiResponse>> CreateTenant([FromBody] CreateTenantRequest request)
    {
        if (request == null)
            return BadRequest(new ApiResponse(false, "Invalid request body")
            {
                ErrorCode = "OC-001"  // Invalid request body
            });

        var defaultResellerId = new Guid("00000000-0000-0000-0000-000000000001");
        var tenant = new Tenant
        {
            ResellerId = Guid.NewGuid(),
            AssignedResellerId = request.AssignedResellerId ?? defaultResellerId,
            DateCreated = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow,
            ThemeConfig = request.ThemeConfig ?? "{}",
            CustomWorkflows = request.CustomWorkflows ?? "[]"
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        return Created($"/admin/tenants/{tenant.ResellerId}", new ApiResponse(true, "Tenant created successfully")
        {
            Data = new { tenant.ResellerId, tenant.AssignedResellerId, tenant.DateCreated },
            ErrorCode = null
        });
    }

    [HttpGet("users")]
    [SwaggerOperation(Summary = "List Users", Description = "Lists users - full access for super admin, tenant-scoped for tenant admin")]
    [SwaggerResponse(200, "Users retrieved", typeof(ApiResponse))]
    public async Task<ActionResult<ApiResponse>> ListUsers()
    {
        var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId);
        if (currentUser == null)
            return NotFound(new ApiResponse(false, "Current user not found")
            {
                ErrorCode = "OC-002"  // Current user not found
            });

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
            Data = users,
            ErrorCode = null
        });
    }

    [HttpPost("users")]
    [SwaggerOperation(Summary = "Create User", Description = "Creates a new user in the tenant")]
    [SwaggerResponse(201, "User created", typeof(ApiResponse))]
    [SwaggerResponse(400, "Invalid request")]
    public async Task<ActionResult<ApiResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var currentUser = await _db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.UserId == currentUserId);
        if (currentUser == null)
            return NotFound(new ApiResponse(false, "Current user not found")
            {
                ErrorCode = "OC-002"  // Current user not found
            });

        var isSuperAdmin = User.IsInRole("super_admin");
        Guid tenantId;
        Guid? assignedResellerId;

        if (isSuperAdmin)
        {
            if (request.TenantId == null)
                return BadRequest(new ApiResponse(false, "TenantId required for super admin")
                {
                    ErrorCode = "OC-003"  // TenantId required
                });

            tenantId = request.TenantId.Value;
            var chosenTenant = await _db.Tenants.FirstOrDefaultAsync(t => t.ResellerId == tenantId);
            if (chosenTenant == null)
                return NotFound(new ApiResponse(false, "Tenant not found")
                {
                    ErrorCode = "OC-004"  // Tenant not found
                });

            assignedResellerId = request.AssignedResellerId ?? chosenTenant.AssignedResellerId;
        }
        else
        {
            tenantId = currentUser.TenantId!.Value;
            assignedResellerId = currentUser.Tenant!.AssignedResellerId;
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == request.Role);
        if (role == null)
            return BadRequest(new ApiResponse(false, "Invalid role")
            {
                ErrorCode = "OC-005"  // Invalid role
            });

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

        // Auto-assign Free plan
        var freePlan = new SubscriptionPlan
        {
            UserId = newUser.UserId,
            PlanType = SubscriptionPlanType.Free,
            MaxActiveLinks = 1,
            IsActive = true,
            ExpiryDate = DateTime.UtcNow.AddYears(10),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.SubscriptionPlans.Add(freePlan);
        await _db.SaveChangesAsync();

        return Created($"/admin/users/{newUser.UserId}", new ApiResponse(true, "User created successfully")
        {
            Data = new { newUser.UserId, newUser.Email },
            ErrorCode = null
        });
    }

    [HttpPost("users/{id:guid}/dependents")]
    [SwaggerOperation(Summary = "Create Dependent User", Description = "Creates a dependent user for the specified parent")]
    [SwaggerResponse(201, "Dependent created")]
    [SwaggerResponse(400, "Invalid request")]
    public async Task<ActionResult<ApiResponse>> CreateDependent(Guid id, [FromBody] CreateDependentRequest request)
    {
        var parent = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id && !u.IsDeleted);
        if (parent == null)
            return NotFound(new ApiResponse(false, "Parent user not found")
            {
                ErrorCode = "OC-006"  // Parent user not found
            });

        var roleName = string.IsNullOrEmpty(request.Role) ? "viewer" : request.Role;
        var userRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == roleName);
        if (userRole == null)
            return BadRequest(new ApiResponse(false, $"Role '{roleName}' not found")
            {
                ErrorCode = "OC-005"  // Invalid role
            });

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

        var freePlan = new SubscriptionPlan
        {
            UserId = dependent.UserId,
            PlanType = SubscriptionPlanType.Free,
            MaxActiveLinks = 1,
            IsActive = true,
            ExpiryDate = DateTime.UtcNow.AddYears(10),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.SubscriptionPlans.Add(freePlan);
        await _db.SaveChangesAsync();

        return Created($"/admin/users/{dependent.UserId}", new ApiResponse(true, "Dependent created successfully")
        {
            Data = new { dependent.UserId, dependent.Email },
            ErrorCode = null
        });
    }

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
            return NotFound(new ApiResponse(false, "User not found")
            {
                ErrorCode = "OC-007"  // User not found
            });

        if (user.ParentId == user.UserId || user.ParentId == null)
            return BadRequest(new ApiResponse(false, "User is not a dependent")
            {
                ErrorCode = "OC-008"  // Not a dependent user
            });

        return Ok(new ApiResponse(true, "User features retrieved")
        {
            Data = user.UserFeatures.Select(uf => new
            {
                uf.FeatureId,
                uf.IsEnabled,
                uf.Right,
                uf.Feature.Key,
                uf.Feature.Name
            }),
            ErrorCode = null
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
            }),
            ErrorCode = null
        });
    }

    [HttpPost("users/{id:guid}/features")]
    [SwaggerOperation(Summary = "Assign Feature to User", Description = "Assigns a feature to a user with enabled/right settings")]
    [SwaggerResponse(200, "Feature assigned")]
    [SwaggerResponse(400, "Invalid request")]
    [SwaggerResponse(404, "User or feature not found")]
    public async Task<ActionResult<ApiResponse>> AssignFeature(Guid id, [FromBody] AssignFeatureRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id && !u.IsDeleted);
        if (user == null)
            return NotFound(new ApiResponse(false, "User not found")
            {
                ErrorCode = "OC-007"  // User not found
            });

        var feature = await _db.Features.FirstOrDefaultAsync(f => f.Id == request.FeatureId);
        if (feature == null)
            return BadRequest(new ApiResponse(false, "Invalid feature")
            {
                ErrorCode = "OC-009"  // Invalid feature
            });

        var existing = await _db.UserFeatures.FirstOrDefaultAsync(uf => uf.UserId == id && uf.FeatureId == request.FeatureId);
        if (existing != null)
            return Conflict(new ApiResponse(false, "Feature already assigned")
            {
                ErrorCode = "OC-010"  // Feature already assigned
            });

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
            },
            ErrorCode = null
        });
    }

    [HttpPatch("users/{id:guid}/features/{featureId:guid}")]
    [SwaggerOperation(Summary = "Update User Feature", Description = "Updates enabled/right settings for a feature assigned to user")]
    [SwaggerResponse(200, "Feature updated")]
    [SwaggerResponse(404, "User feature not found")]
    public async Task<ActionResult<ApiResponse>> UpdateFeature(Guid id, Guid featureId, [FromBody] UpdateFeatureRequest request)
    {
        var userFeature = await _db.UserFeatures.FirstOrDefaultAsync(uf => uf.UserId == id && uf.FeatureId == featureId);
        if (userFeature == null)
            return NotFound(new ApiResponse(false, "User feature not found")
            {
                ErrorCode = "OC-011"  // User feature not found
            });

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
            },
            ErrorCode = null
        });
    }

    [HttpDelete("users/{id:guid}/features/{featureId:guid}")]
    [SwaggerOperation(Summary = "Remove User Feature", Description = "Removes a feature assignment from the user")]
    [SwaggerResponse(200, "Feature removed")]
    [SwaggerResponse(404, "User feature not found")]
    public async Task<ActionResult<ApiResponse>> RemoveFeature(Guid id, Guid featureId)
    {
        var userFeature = await _db.UserFeatures.FirstOrDefaultAsync(uf => uf.UserId == id && uf.FeatureId == featureId);
        if (userFeature == null)
            return NotFound(new ApiResponse(false, "User feature not found")
            {
                ErrorCode = "OC-011"  // User feature not found
            });

        _db.UserFeatures.Remove(userFeature);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Feature removed successfully")
        {
            ErrorCode = null
        });
    }

    [HttpPost("resellers")]
    [Authorize(Roles = "super_admin")]
    [SwaggerOperation(Summary = "Create Reseller", Description = "Creates a new Reseller account. Super admin only.")]
    [SwaggerResponse(201, "Reseller created")]
    [SwaggerResponse(400, "Invalid request")]
    [SwaggerResponse(409, "Email already in use")]
    public async Task<ActionResult<ApiResponse>> CreateReseller([FromBody] CreateResellerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ApiResponse(false, "Name and Email are required")
            {
                ErrorCode = "OC-001"  // Required fields missing
            });

        var existing = await _db.Resellers.AnyAsync(r => r.Email == request.Email);
        if (existing)
            return Conflict(new ApiResponse(false, "Email already in use")
            {
                ErrorCode = "OC-012"  // Email already in use
            });

        var tempPassword = Guid.NewGuid().ToString("N").Substring(0, 12);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

        var reseller = new Reseller
        {
            ResellerId = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            ContactName = request.ContactName,
            PhoneNumber = request.PhoneNumber,
            CreatedByAdminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Resellers.Add(reseller);
        await _db.SaveChangesAsync();

        // TODO: Send email with tempPassword

        return Created($"/admin/resellers/{reseller.ResellerId}", new ApiResponse(true, "Reseller created")
        {
            Data = new
            {
                reseller.ResellerId,
                reseller.Name,
                reseller.Email,
                TemporaryPassword = tempPassword // Remove in production
            },
            ErrorCode = null
        });
    }

    [HttpGet("resellers")]
    [Authorize(Roles = "super_admin")]
    [SwaggerOperation(Summary = "List Resellers", Description = "Returns all Resellers with basic info and tenant count.")]
    [SwaggerResponse(200, "Resellers retrieved")]
    public async Task<ActionResult<ApiResponse>> ListResellers()
    {
        var resellers = await _db.Resellers
            .Select(r => new
            {
                r.ResellerId,
                r.Name,
                r.Email,
                r.ContactName,
                r.PhoneNumber,
                r.IsActive,
                r.CreatedAt,
                tenantCount = _db.Tenants.Count(t => t.AssignedResellerId == r.ResellerId)
            })
            .ToListAsync();

        return Ok(new ApiResponse(true, "Resellers retrieved")
        {
            Data = resellers,
            ErrorCode = null
        });
    }

    [HttpPatch("tenants/{tenantId:guid}/reseller")]
    [Authorize(Roles = "super_admin")]
    [SwaggerOperation(Summary = "Move Tenant to Reseller", Description = "Reassigns a Tenant to a different Reseller.")]
    [SwaggerResponse(200, "Tenant reassigned")]
    [SwaggerResponse(404, "Tenant or Reseller not found")]
    public async Task<ActionResult<ApiResponse>> MoveTenantToReseller(Guid tenantId, [FromBody] MoveTenantRequest request)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.ResellerId == tenantId);
        if (tenant == null)
            return NotFound(new ApiResponse(false, "Tenant not found")
            {
                ErrorCode = "OC-004"  // Tenant not found
            });

        var newReseller = await _db.Resellers.FirstOrDefaultAsync(r => r.ResellerId == request.NewResellerId);
        if (newReseller == null)
            return NotFound(new ApiResponse(false, "New Reseller not found")
            {
                ErrorCode = "OC-013"  // New Reseller not found
            });

        tenant.AssignedResellerId = request.NewResellerId;
        tenant.DateUpdated = DateTime.UtcNow;

        _db.Tenants.Update(tenant);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Tenant reassigned successfully")
        {
            Data = new { tenant.ResellerId, newResellerId = request.NewResellerId },
            ErrorCode = null
        });
    }
}