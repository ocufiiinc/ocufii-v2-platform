namespace OcufiiAPI.Configs
{
    public class LegacyConfig
    {
        public string FixedTenantId { get; set; } = string.Empty;
        public string DefaultRole { get; set; } = "viewer";
        public string LegacyRole { get; set; } = "legacy_user";

        public string RegistrationRole { get; set; } = "viewer";
    }
}
