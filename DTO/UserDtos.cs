namespace OcufiiAPI.DTO
{
    public class UpdateProfileDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Company { get; set; }
    }

    public class UpdateUserStatusDto
    {
        public bool IsEnabled { get; set; }
    }

    public class UpdateSettingsDto
    {
        public bool? MovementSound { get; set; }
        public bool? MovementVibration { get; set; }
        public string? NotificationSound { get; set; } 
        public bool? AutoLogoutEnabled { get; set; }
        public int? AutoLogoutInterval { get; set; } 
        public bool? BypassFocus { get; set; }
        public string? PersonalSafetyUsername { get; set; }
    }

    public class UpdateAssistDto
    {
        public bool? AlarmSound { get; set; }
        public string? AlertMessage { get; set; }
        public bool? FlashOn { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? ScreenFlashing { get; set; }
    }

    public class AcceptTermsDto
    {
        public Guid TosId { get; set; }
        public string TosVersion { get; set; } = string.Empty;
    }
}
