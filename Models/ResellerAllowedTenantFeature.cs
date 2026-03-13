namespace OcufiiAPI.Models
{
    public class ResellerAllowedTenantFeature
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ResellerId { get; set; }
        public Reseller Reseller { get; set; } = null!;
        public Guid FeatureId { get; set; }
        public Feature Feature { get; set; } = null!;
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
