using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.Enums;
using System.Security.Claims;

namespace OcufiiAPI.Services;

public class PermissionService
{
    private readonly OcufiiDbContext _db;

    public PermissionService(OcufiiDbContext db)
    {
        _db = db;
    }

    public async Task<bool> CanPerformAsync(
        ClaimsPrincipal user,
        string featureKey,
        FeatureRight requiredRight)
    {
        var userType = user.FindFirst("user_type")?.Value;
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userType) || string.IsNullOrEmpty(userIdStr))
            return false;

        var userId = Guid.Parse(userIdStr);

        if (userType == "platform")
        {
            var admin = await _db.PlatformAdmins.FindAsync(userId);
            if (admin == null) return false;

            if (admin.Role == "super_admin" || admin.Role == "Administrator - Full Access")
                return true;

            return await _db.PlatformAdminFeatures
                .AnyAsync(paf =>
                    paf.AdminId == userId &&
                    paf.Feature.Key == featureKey &&
                    paf.IsEnabled &&
                    (int)paf.Right >= (int)requiredRight);
        }
        else if (userType == "reseller")
        {
            if (featureKey == "tenant_management" || featureKey == "reporting")
                return true;

            return false;
        }

        return false;
    }

    public async Task<bool> IsDefaultSuperAdminAsync(ClaimsPrincipal user)
    {
        var userType = user.FindFirst("user_type")?.Value;
        if (userType != "platform") return false;

        var adminId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var admin = await _db.PlatformAdmins.FindAsync(adminId);
        return admin?.Email.Equals("superadmin@ocufii.com", StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task<bool> IsDefaultResellerAsync(ClaimsPrincipal user)
    {
        var userType = user.FindFirst("user_type")?.Value;
        if (userType != "reseller") return false;

        var resellerId = Guid.Parse(user.FindFirst("reseller_id")?.Value!);
        var reseller = await _db.Resellers.FindAsync(resellerId);
        return reseller?.Email.Equals("defaultReseller@ocufii.com", StringComparison.OrdinalIgnoreCase) == true;
    }
}