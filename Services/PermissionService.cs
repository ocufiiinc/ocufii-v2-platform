using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.Enums;
using OcufiiAPI.Models;

namespace OcufiiAPI.Services;

public class PermissionService
{
    private readonly OcufiiDbContext _db;

    public PermissionService(OcufiiDbContext db)
    {
        _db = db;
    }

    public async Task<bool> CanPerformAsync(Guid currentAdminId, string featureKey, FeatureRight requiredRight)
    {
        var admin = await _db.PlatformAdmins.FindAsync(currentAdminId);
        if (admin == null) return false;

        if (admin.Role == "super_admin") return true;

        return await _db.PlatformAdminFeatures
            .AnyAsync(paf =>
                paf.AdminId == currentAdminId &&
                paf.Feature.Key == featureKey &&
                paf.IsEnabled &&
                (int)paf.Right >= (int)requiredRight); 
    }

    public async Task<bool> IsDefaultSuperAdminAsync(Guid adminId)
    {
        var admin = await _db.PlatformAdmins.FindAsync(adminId);
        return admin?.Email.Equals("superadmin@ocufii.com", StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task<bool> IsDefaultResellerAsync(Guid resellerId)
    {
        var reseller = await _db.Resellers.FindAsync(resellerId);
        return reseller?.Email.Equals("defaultReseller@ocufii.com", StringComparison.OrdinalIgnoreCase) == true;
    }
}