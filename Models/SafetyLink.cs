using OcufiiAPI.Enums;

namespace OcufiiAPI.Models
{
    public class SafetyLink
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SenderId { get; set; }  // Invitee
        public Guid RecipientId { get; set; }  // Recipient
        public SafetyLinkStatus Status { get; set; } = SafetyLinkStatus.Pending;
        public string AliasName { get; set; } = string.Empty;
        public bool EnableLocation { get; set; } = false;
        public bool EnableSafety { get; set; } = false;
        public bool EnableSecurity { get; set; } = false;
        public bool Snooze { get; set; } = false;
        public DateTime? SnoozeStartTime { get; set; }
        public DateTime? SnoozeEndTime { get; set; }
        public string OTP { get; set; } = string.Empty;
        public DateTime? OTPExpiry { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
