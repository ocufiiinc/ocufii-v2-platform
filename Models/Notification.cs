using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class Notification
    {
        [Key]
        public Guid NotificationId { get; set; }

        [Key]
        public Guid UserId { get; set; }

        [Key]
        [Column(TypeName = "timestamp with time zone")]
        public DateTime NotificationTimestamp { get; set; }

        [StringLength(255)]
        public string? Title { get; set; }

        public string? Body { get; set; }

        [StringLength(50)]
        public string? NotificationType { get; set; }

        [StringLength(50)]
        public string? NotificationCategory { get; set; }

        [StringLength(100)]
        public string? NotificationReason { get; set; }

        [StringLength(50)]
        public string? NotificationStatus { get; set; }

        public int? Priority { get; set; }

        [StringLength(50)]
        public string? Sound { get; set; }

        public bool? ContentAvailable { get; set; }
        public bool? Acknowledge { get; set; }
        public bool? IsSnoozed { get; set; }

        public int? Battery { get; set; }

        [StringLength(50)]
        public string? BeaconMac { get; set; }

        [StringLength(50)]
        public string? GatewayMac { get; set; }

        [StringLength(50)]
        public string? DeviceMac { get; set; }

        [StringLength(100)]
        public string? DataUuid { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? RecordTimestamp { get; set; }

        public User User { get; set; } = null!;
    }
}
