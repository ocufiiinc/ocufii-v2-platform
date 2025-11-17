using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class AuditLog
    {
        [Key]
        public Guid LogId { get; set; }

        [Key]
        [Column(TypeName = "timestamp with time zone")]
        public DateTime Timestamp { get; set; }

        public Guid? UserId { get; set; }
        public Guid? TenantId { get; set; }

        [StringLength(100)]
        public string? Action { get; set; }

        public Guid? ResourceId { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Details { get; set; }

        public User? User { get; set; }
        public Tenant? Tenant { get; set; }
    }
}
