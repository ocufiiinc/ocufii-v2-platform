using Microsoft.EntityFrameworkCore;
using OcufiiAPI.Data;
using OcufiiAPI.Models;
using System.Security.Claims;

namespace OcufiiAPI.Services
{
    public class PermissionService
    {
        private readonly OcufiiDbContext _db;

        public PermissionService(OcufiiDbContext db)
        {
            _db = db;
        }

        public async Task<bool> CanPerformAsync(ClaimsPrincipal user, string permissionKey)
        {
            var userType = user.FindFirst("user_type")?.Value;
            var principalIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userType) || string.IsNullOrEmpty(principalIdStr))
                return false;

            var principalId = Guid.Parse(principalIdStr);

            var permission = await _db.Permissions.FirstOrDefaultAsync(p => p.Key == permissionKey);
            if (permission == null) return false;

            // Layer 0: Role ceiling
            var roleCeiling = await GetRoleCeiling(userType, permission.PermissionId);
            if (roleCeiling == null || !roleCeiling.IsGranted) return false;

            // Layer 1: Configured permission
            var isGranted = await IsPermissionGranted(userType, principalId, permission.PermissionId);
            return isGranted;
        }

        private async Task<RolePermission?> GetRoleCeiling(string userType, Guid permissionId)
        {
            int roleId = userType switch
            {
                "platform" => 1,
                "reseller" => 2,
                "tenant" => 3,
                _ => 4
            };

            return await _db.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
        }

        private async Task<bool> IsPermissionGranted(string userType, Guid principalId, Guid permissionId)
        {
            return userType switch
            {
                "platform" => await _db.PlatformPermissions.AnyAsync(p => p.AdminId == principalId && p.PermissionId == permissionId && p.IsGranted),
                "reseller" => await _db.ResellerPermissions.AnyAsync(p => p.ResellerId == principalId && p.PermissionId == permissionId && p.IsGranted),
                "tenant" => await _db.TenantPermissions.AnyAsync(p => p.TenantId == principalId && p.PermissionId == permissionId && p.IsGranted),
                _ => false
            };
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
}