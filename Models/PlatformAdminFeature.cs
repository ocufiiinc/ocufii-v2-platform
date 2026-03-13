using OcufiiAPI.Enums;

namespace OcufiiAPI.Models
{
    public class PlatformAdminFeature
    {
        public Guid Id { get; set; }
        public Guid AdminId { get; set; }
        public PlatformAdmin Admin { get; set; } = null!;
        public Guid FeatureId { get; set; }
        public Feature Feature { get; set; } = null!;
        public bool IsEnabled { get; set; }
        public bool OnlyView { get; set; } = false;
        public bool CanEdit { get; set; } = false;
        public bool FullAccess { get; set; } = false;
        public bool CanCreate { get; set; } = false;
        public bool CanDelete { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
