using OcufiiAPI.Enums;

namespace OcufiiAPI.Models
{
    public class SubscriptionPlan
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public SubscriptionPlanType PlanType { get; set; } = SubscriptionPlanType.Free;
        public int MaxActiveLinks { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public DateTime ExpiryDate { get; set; } = DateTime.UtcNow.AddYears(1);
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
