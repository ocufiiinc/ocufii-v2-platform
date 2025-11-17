using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class SnoozeSetting
    {
        [Key]
        public Guid UserId { get; set; }

        [Key, StringLength(50)]
        public string BeaconMac { get; set; } = string.Empty;

        [StringLength(50)]
        public string? GatewayMac { get; set; }

        public int? SnoozeDuration { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? SnoozeTimestampStart { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? SnoozeTimestampEnd { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public Beacon Beacon { get; set; } = null!;
    }
}
