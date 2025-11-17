using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class CustomerSupportTicket
    {
        [Key]
        public Guid TicketId { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        [StringLength(100)]
        public string? Name { get; set; }

        public string? Message { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Attachments { get; set; }

        [StringLength(50)]
        public string? Status { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
        public bool IsSent { get; set; } = false;

        public User User { get; set; } = null!;
    }
}
