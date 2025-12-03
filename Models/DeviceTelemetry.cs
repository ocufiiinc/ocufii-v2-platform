namespace OcufiiAPI.Models
{
    public class DeviceTelemetry
    {
        public long Id { get; set; }  
        public Guid DeviceId { get; set; }
        public TelemetrySource SourceType { get; set; }
        public Guid? ViaDeviceId { get; set; }
        public short? BatteryLevel { get; set; }
        public short? SignalStrength { get; set; }
        public string? SignalQuality { get; set; }
        public string Payload { get; set; } = "{}"; 
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DeviceTimestamp { get; set; }

        public Device Device { get; set; } = null!;
        public Device? ViaDevice { get; set; }
    }
}
