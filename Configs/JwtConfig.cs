namespace OcufiiAPI.Configs
{
    public class JwtConfig
    {
        public string Secret { get; set; } = "2b225b2d3ab4c9148843cdda608e2623";
        public string Issuer { get; set; } = "OcufiiAPI";
        public string Audience { get; set; } = "OcufiiClients";
        public int ExpiryMinutes { get; set; } = 60;
        public int AccessTokenMinutes { get; set; } = 60;
        public int RefreshTokenDays { get; set; } = 30;
    }
}
