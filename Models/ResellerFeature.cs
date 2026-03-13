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
        public bool OnlyView { get; set; } = false;
        public bool CanEdit { get; set; } = false;
        public bool FullAccess { get; set; } = false;
        public bool CanCreate { get; set; } = false;
        public bool CanDelete { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
