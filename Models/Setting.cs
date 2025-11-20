using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class Setting
    {
        public Guid UserId { get; set; }
        public bool MovementSound { get; set; } = true;
        public bool MovementVibration { get; set; } = true;
        public string NotificationSound { get; set; } = "DEFAULT";
        public bool AutoLogoutEnabled { get; set; } = false;
        public int AutoLogoutInterval { get; set; } = 15;
        public bool BypassFocus { get; set; } = false;
        public string? PersonalSafetyUsername { get; set; }

        // ToS
        public Guid? TosId { get; set; }
        public string? TosVersion { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }

        // Assist Settings (JSON column)
        public string? AssistSettings { get; set; } = "{}"; // JSON string

        public User User { get; set; } = null!;
    }
}
