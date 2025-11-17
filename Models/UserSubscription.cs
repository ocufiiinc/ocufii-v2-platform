using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OcufiiAPI.Models
{
    public class UserSubscription
    {
        [Key]
        public Guid SubscriptionUserId { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        [StringLength(100)]
        public string SubscriptionId { get; set; } = string.Empty;

        [StringLength(10)]
        public string? OsType { get; set; } // android, ios

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? StartTime { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? ExpiryTime { get; set; }

        public bool? AutoRenewEnabled { get; set; }
        public bool IsCanceled { get; set; } = false;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? CancelTime { get; set; }

        [StringLength(50)]
        public string? SubscriptionState { get; set; }

        [StringLength(50)]
        public string? SubscriptionPlanType { get; set; }

        [StringLength(50)]
        public string? PromoCode { get; set; }

        [StringLength(10)]
        public string? RegionCode { get; set; }

        [StringLength(50)]
        public string? AcknowledgementState { get; set; }

        [StringLength(100)]
        public string? LatestOrderId { get; set; }

        [StringLength(255)]
        public string? LinkedPurchaseToken { get; set; }

        [StringLength(100)]
        public string? AppAppleId { get; set; }

        [StringLength(100)]
        public string? BundleId { get; set; }

        [StringLength(50)]
        public string? BundleVersion { get; set; }

        [StringLength(50)]
        public string? DeviceType { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? ExpiresDate { get; set; }

        [StringLength(50)]
        public string? NotificationSubType { get; set; }

        [StringLength(50)]
        public string? NotificationType { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? OriginalPurchaseDate { get; set; }

        [StringLength(100)]
        public string? ProductId { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? PurchaseDate { get; set; }

        public User User { get; set; } = null!;
        public Subscription Subscription { get; set; } = null!;
    }
}
