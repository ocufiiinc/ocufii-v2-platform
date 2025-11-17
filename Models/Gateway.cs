using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class Gateway
    {
        [Key]
        public string GatewayMac { get; set; } = string.Empty;

        [Key]
        public Guid UserId { get; set; }

        [StringLength(100)]
        public string? DeviceName { get; set; }

        [StringLength(255)]
        public string? GatewayLocation { get; set; }

        [StringLength(50)]
        public string? GatewayType { get; set; }

        [StringLength(50)]
        public string? Firmware { get; set; }

        [StringLength(255)]
        public string? QrCode { get; set; }

        [Column(TypeName = "jsonb")]
        public string? WifiSettings { get; set; }

        public string? Comments { get; set; }

        [Column(TypeName = "jsonb")]
        public string? ConfigurationDetail { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
        public bool IsEnabled { get; set; } = true;
        public bool IsSync { get; set; } = false;

        [StringLength(50)]
        public string? DeviceType { get; set; }

        public User User { get; set; } = null!;
        public ICollection<GatewayData> GatewayData { get; set; } = new List<GatewayData>();
        public ICollection<GatewayBeacon> GatewayBeacons { get; set; } = new List<GatewayBeacon>();
        public ICollection<Thing> Things { get; set; } = new List<Thing>();
        public ICollection<UserGateway> UserGateways { get; set; } = new List<UserGateway>();
    }
}
