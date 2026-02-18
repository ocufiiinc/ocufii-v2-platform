namespace OcufiiAPI.Configs
{
    public class MqttConfig
    {
        public string Host { get; set; } = "172.19.92.122";
        public int Port { get; set; } = 8883;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseTls { get; set; } = false;
    }
}
