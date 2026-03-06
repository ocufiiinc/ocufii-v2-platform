using OcufiiAPI.Enums;

namespace OcufiiAPI.Models
{
    public class ResellerFeature
    {
        public Guid Id { get; set; }
        public Guid ResellerId { get; set; }
        public Reseller Reseller { get; set; } = null!;
        public Guid FeatureId { get; set; }
        public Feature Feature { get; set; } = null!;
        public bool IsEnabled { get; set; } = true;
        public FeatureRight Right { get; set; } = FeatureRight.FullAccess;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
