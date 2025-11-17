using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class GatewayBeacon
    {
        [Key]
        public Guid GatewayUserId { get; set; }

        [Key, StringLength(50)]
        public string GatewayMac { get; set; } = string.Empty;

        [Key]
        public Guid BeaconUserId { get; set; }

        [Key, StringLength(50)]
        public string BeaconMac { get; set; } = string.Empty;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateAssociated { get; set; } = DateTime.UtcNow;

        public Gateway Gateway { get; set; } = null!;
        public Beacon Beacon { get; set; } = null!;
    }
}
