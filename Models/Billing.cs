using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OcufiiAPI.Models
{
    public class Billing
    {
        [Key]
        public Guid BillingId { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        public string? SubscriptionId { get; set; }

        public decimal? Amount { get; set; }

        [StringLength(50)]
        public string? Status { get; set; } // pending, paid, overdue

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? InvoicePeriodStart { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? InvoicePeriodEnd { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Tenant Tenant { get; set; } = null!;
        public Subscription? Subscription { get; set; }
        public Invoice? Invoice { get; set; }
    }
}
