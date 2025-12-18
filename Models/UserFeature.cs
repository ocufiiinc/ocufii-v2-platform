namespace OcufiiAPI.Models
{
    public class UserFeature
    {
        public Guid UserId { get; set; }
        public Guid FeatureId { get; set; }
        public bool IsEnabled { get; set; } = false;
        public FeatureRight Right { get; set; } = FeatureRight.OnlyView;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
        public Feature Feature { get; set; } = null!;
    }
}
