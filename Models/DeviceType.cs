namespace OcufiiAPI.Models
{
    public class DeviceType
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool ConnectsToMqtt { get; set; } = false;
        public bool RequiresAuth { get; set; } = false;
        public bool IsUserVisible { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
