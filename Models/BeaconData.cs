using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class BeaconData
    {
        [Key]
        public Guid UserId { get; set; }

        [Key]
        [StringLength(50)]
        public string BeaconMac { get; set; } = string.Empty;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateUpdated { get; set; }

        [StringLength(50)]
        public string? GatewayMac { get; set; }

        public int? Battery { get; set; }

        [StringLength(255)]
        public string? BeaconValue { get; set; }

        public int? SignalStrength { get; set; }

        public Beacon Beacon { get; set; } = null!;
    }
}
