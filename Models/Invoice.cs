using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OcufiiAPI.Models
{
    public class Invoice
    {
        [Key]
        public Guid InvoiceId { get; set; } = Guid.NewGuid();

        public Guid BillingId { get; set; }

        [StringLength(500)]
        public string? PdfUrl { get; set; }

        public decimal? TotalAmount { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? PaidAt { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Billing Billing { get; set; } = null!;
    }
}
