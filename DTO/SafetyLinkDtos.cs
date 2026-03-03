using OcufiiAPI.Enums;

namespace OcufiiAPI.DTO
{
    public class SendInvitationDto
    {
        public string Email { get; set; } = string.Empty;
        public string AliasName { get; set; } = string.Empty;
        public bool EnableLocation { get; set; } = false;
        public bool EnableSafety { get; set; } = false;
        public bool EnableSecurity { get; set; } = false;
    }

    public class UpdateLinkedDto
    {
        public string? AliasName { get; set; }
        public bool? EnableLocation { get; set; }
        public bool? EnableSafety { get; set; }
        public bool? EnableSecurity { get; set; }
    }

    public class SnoozeDto
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class UpdateStatusDto
    {
        public string Status { get; set; } = string.Empty; 
    }

    public class AcceptInvitationDto
    {
        public string OTP { get; set; } = string.Empty;
    }

    public class LinkedMemberDto
    {
        public Guid LinkId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string AliasName { get; set; } = string.Empty;
        public SafetyLinkStatus Status { get; set; }
        public bool EnableLocation { get; set; }
        public bool EnableSafety { get; set; }
        public bool EnableSecurity { get; set; }
        public bool Snooze { get; set; }
        public DateTime? SnoozeStartTime { get; set; }
        public DateTime? SnoozeEndTime { get; set; }
        public bool IsOtpExpired { get; set; }
    }
}
