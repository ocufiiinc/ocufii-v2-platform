namespace OcufiiAPI.Models
{
    public class UserAssistSetting
    {
        public Guid UserId { get; set; }

        public string Config { get; set; } = "{}"; 

        public string? PersonalSafetyUsername { get; set; }

        public User User { get; set; } = null!;
    }
}
