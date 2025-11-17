using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class Beacon
    {
        [Key]
        public string BeaconMac { get; set; } = string.Empty;

        [Key]
        public Guid UserId { get; set; }

        [StringLength(100)]
        public string? BeaconName { get; set; }

        [StringLength(255)]
        public string? BeaconLocation { get; set; }

        [StringLength(50)]
        public string? BeaconType { get; set; }

        public string? Comments { get; set; }

        [Column(TypeName = "jsonb")]
        public string? ConfigurationDetail { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
        public bool IsEnabled { get; set; } = true;

        [StringLength(50)]
        public string? DeviceType { get; set; } // FlexiTag, FlexiBand, etc.

        public User User { get; set; } = null!;
        public ICollection<BeaconData> BeaconData { get; set; } = new List<BeaconData>();
        public ICollection<GatewayBeacon> GatewayBeacons { get; set; } = new List<GatewayBeacon>();
        public ICollection<SnoozeSetting> SnoozeSettings { get; set; } = new List<SnoozeSetting>();
    }
}
