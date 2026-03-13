using OcufiiAPI.Enums;

namespace OcufiiAPI.Models
{
    public class Feature
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? DeviceTypeId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public FeatureType FeatureType { get; set; } = FeatureType.Platform;
        public DeviceType? DeviceType { get; set; }
    }
}
