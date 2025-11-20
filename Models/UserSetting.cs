namespace OcufiiAPI.Models
{
    public enum NotificationSoundType
    {
        DEFAULT,
        FIRE,
        EMERGENCY
    }

    public class UserSetting
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        public bool MovementSound { get; set; } = true;
        public bool MovementVibration { get; set; } = true;
        public NotificationSoundType NotificationSound { get; set; } = NotificationSoundType.DEFAULT;

        public bool AutoLogoutEnabled { get; set; } = false;
        public int AutoLogoutInterval { get; set; } = 15;

        public bool BypassFocus { get; set; } = false;

        // This column exists in your DB as personal_safety_username
        public string? PersonalSafetyUsername { get; set; }

        public Guid? TosId { get; set; }
        public string? TosVersion { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User User { get; set; } = null!;
    }
}
