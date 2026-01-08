namespace OcufiiAPI.Models
{
    public class Device
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DeviceTypeId { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Location { get; set; }
        public string? Information { get; set; }
        public string Attributes { get; set; } = "{}"; // jsonb
        public Guid? UserId { get; set; }
        public Guid? TenantId { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeviceTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DeviceType DeviceType { get; set; } = null!;
        public User? User { get; set; }
    }

    public class DeviceCredential
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DeviceId { get; set; }
        public string MqttUsername { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public DateTime? LastRotatedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Device Device { get; set; } = null!;
    }

    public class VerifyCredentialsRequest
    {
        public string MqttUsername { get; set; } = string.Empty;
        public string MqttPassword { get; set; } = string.Empty;
    }
}
