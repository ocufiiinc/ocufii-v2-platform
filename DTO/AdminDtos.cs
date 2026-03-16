using OcufiiAPI.Enums;
using OcufiiAPI.Models;
namespace OcufiiAPI.DTO
{
    public class PermissionAssignmentDto
    {
        public Guid PermissionId { get; set; }
        public bool IsGranted { get; set; } = false;
    }

    public class FeatureAssignmentDto
    {
        public Guid FeatureId { get; set; }
        public bool IsEnabled { get; set; } = false;
    }

    public class UpdateSystemPermissionDto
    {
        public bool? IsDefault { get; set; }
    }

    public class CreatePlatformUserDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public List<PermissionAssignmentDto>? Permissions { get; set; }
    }

    public class UpdatePlatformPermissionsDto
    {
        public List<PermissionAssignmentDto>? Permissions { get; set; }
    }

    public class CreateResellerDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ContactName { get; set; }
        public string? PhoneNumber { get; set; }
        public List<PermissionAssignmentDto>? Permissions { get; set; }
        public List<FeatureAssignmentDto>? Features { get; set; }
    }

    public class UpdateResellerDto
    {
        public string? Name { get; set; }
        public string? ContactName { get; set; }
        public string? PhoneNumber { get; set; }
        public List<PermissionAssignmentDto>? Permissions { get; set; }
        public List<FeatureAssignmentDto>? Features { get; set; }
    }

    public class CreateTenantDto
    {
        public string OwnerEmail { get; set; } = string.Empty;
        public string OwnerFirstName { get; set; } = string.Empty;
        public string? OwnerLastName { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public List<PermissionAssignmentDto>? Permissions { get; set; }
        public List<FeatureAssignmentDto>? Features { get; set; }
    }

    public class UpdateTenantDto
    {
        public string? Name { get; set; }
        public List<PermissionAssignmentDto>? Permissions { get; set; }
        public List<FeatureAssignmentDto>? Features { get; set; }
    }

    public class AdminUpdateStatusDto
    {
        public bool IsActive { get; set; }
    }

    public class MoveTenantDto
    {
        public Guid NewResellerId { get; set; }
    }
}