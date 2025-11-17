using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class PromoGroup
    {
        [Key]
        public Guid PromoGroupId { get; set; } = Guid.NewGuid();

        [Required, StringLength(100)]
        public string PromoGroupName { get; set; } = string.Empty;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public int? Duration { get; set; }
        public int? NumberOfPromos { get; set; }

        public string? SubscriptionId { get; set; }

        public Subscription? Subscription { get; set; }
        public ICollection<PromoCode> PromoCodes { get; set; } = new List<PromoCode>();
    }
}
