namespace OcufiiAPI.Configs
{
    public class SettingsDefaultsConfig
    {
        public bool MovementSound { get; set; } = true;
        public bool MovementVibration { get; set; } = true;
        public string NotificationSound { get; set; } = "DEFAULT";
        public bool AutoLogoutEnabled { get; set; } = false;
        public int AutoLogoutInterval { get; set; } = 15;
        public bool BypassFocus { get; set; } = false;
        public List<string> AllowedNotificationSounds { get; set; } = new() { "DEFAULT", "FIRE", "EMERGENCY" };
        public int MinAutoLogoutInterval { get; set; } = 5;
        public int MaxAutoLogoutInterval { get; set; } = 120;
    }

    public class AssistDefaultsConfig
    {
        public Dictionary<string, object> Config { get; set; } = new();
    }
}
