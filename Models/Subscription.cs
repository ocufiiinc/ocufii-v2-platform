using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class Subscription
    {
        [Key, StringLength(100)]
        public string SubscriptionId { get; set; } = string.Empty;

        public int? Duration { get; set; }
        public bool IsEnable { get; set; } = true;
        public int? NoOfBeacons { get; set; }
        public int? NoOfGateways { get; set; }
        public string? PlanId { get; set; }
        public decimal? Price { get; set; }
        public bool? Sms { get; set; }

        [StringLength(50)]
        public string? SubscriptionType { get; set; }

        public ICollection<Billing> Billings { get; set; } = new List<Billing>();
        public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    }
}
