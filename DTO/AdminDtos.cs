using OcufiiAPI.Models;

namespace OcufiiAPI.DTO
{
    public class CreateTenantRequest
    {
        public string? ThemeConfig { get; set; }
        public Guid? AssignedResellerId { get; set; }
        public string? CustomWorkflows { get; set; }
    }

    public class CreateUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Company { get; set; }
        public string Role { get; set; } = "user";
        public Guid? AssignedResellerId { get; set; }
        public Guid? TenantId { get; set; } 
    }

    public class CreateDependentRequest : CreateUserRequest
    {
        public new string Email { get; set; } = string.Empty;
        public new string FirstName { get; set; } = string.Empty;
        public new string LastName { get; set; } = string.Empty;
        public new string? PhoneNumber { get; set; }
        public new string? Company { get; set; }
        public new string? Role { get; set; } = "user";
        public List<AssignFeatureRequest>? Features { get; set; }  
    }

    public class AssignFeatureRequest
    {
        public Guid FeatureId { get; set; }
        public bool IsEnabled { get; set; } = true;
        public FeatureRight Right { get; set; } = FeatureRight.OnlyView;
    }

    public class UpdateFeatureRequest
    {
        public bool IsEnabled { get; set; }
        public FeatureRight Right { get; set; }
    }

    public class MoveTenantRequest
    {
        public Guid NewResellerId { get; set; }
    }

    public class CreateResellerRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ContactName { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
