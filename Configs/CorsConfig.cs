namespace OcufiiAPI.Configs
{
    public class CorsConfig
    {
        public string[] AllowedOrigins { get; set; } = { "https://ocufii.com", "http://localhost:3000" };
    }
}
