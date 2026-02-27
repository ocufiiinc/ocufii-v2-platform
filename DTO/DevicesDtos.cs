namespace OcufiiAPI.DTO
{
    public class CreateDeviceRequest
    {
        public string Type { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Location { get; set; }
        public string? Information { get; set; }
        public string? Attributes { get; set; } = "{}";
    }

    public class UpdateDeviceRequest
    {
        public string? Name { get; set; }
        public string? Location { get; set; }
        public bool? IsEnabled { get; set; }
        public string? Information { get; set; }
        public string? Attributes { get; set; } = "{}"; 
    }

    public class IssueCredentialsRequest
    {
        public bool Regenerate { get; set; } = false;
    }

    public class VerifyCredentialsRequest
    {
        public string MqttUsername { get; set; } = string.Empty;
        public string MqttPassword { get; set; } = string.Empty;
    }
}
