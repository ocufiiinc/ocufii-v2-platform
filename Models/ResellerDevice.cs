using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class ResellerDevice
    {
        [Key]
        public Guid ResellerId { get; set; }

        [Key]
        public Guid Id { get; set; }

        [StringLength(50)]
        public string? GatewayMac { get; set; }

        [StringLength(50)]
        public string? BeaconMac { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateUpdated { get; set; } = DateTime.UtcNow;

        public Tenant Tenant { get; set; } = null!;
    }
}
