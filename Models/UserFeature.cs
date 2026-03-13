using OcufiiAPI.Enums;

namespace OcufiiAPI.Models
{
    public class UserFeature
    {
        public Guid UserId { get; set; }
        public Guid FeatureId { get; set; }
        public bool IsEnabled { get; set; } = false;
        public bool OnlyView { get; set; } = false;
        public bool CanEdit { get; set; } = false;
        public bool FullAccess { get; set; } = false;
        public bool CanCreate { get; set; } = false;
        public bool CanDelete { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
        public Feature Feature { get; set; } = null!;
    }
}
