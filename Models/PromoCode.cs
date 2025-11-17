using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class PromoCode
    {
        [Key, StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public int? Duration { get; set; }

        [StringLength(255)]
        public string? Email { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? ExpirationTime { get; set; }

        public bool IsUsed { get; set; } = false;

        [StringLength(10)]
        public string? OsType { get; set; }

        [StringLength(50)]
        public string? PromoStatus { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? StartDate { get; set; }

        public string? SubscriptionId { get; set; }

        public Guid? PromoGroupId { get; set; }

        public bool IsPromotional { get; set; } = false;

        public Subscription? Subscription { get; set; }
        public PromoGroup? PromoGroup { get; set; }
    }
}
