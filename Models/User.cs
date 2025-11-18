using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OcufiiAPI.Models
{
    public class User
    {
        [Key]
        public Guid UserId { get; set; } = Guid.NewGuid();

        [Required, EmailAddress, StringLength(255)]
        public string Email { get; set; } = string.Empty;

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }

        [Required, StringLength(255)]
        public string Password { get; set; } = string.Empty;

        public string? Company { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? DateSubmitted { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateUpdated { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
        public bool IsEnabled { get; set; } = true;
        public bool IsLockedFromTwoStep { get; set; } = false;
        public int RetryCount { get; set; } = 0;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? RetryTime { get; set; }

        public int RoleId { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? SubscriptionDate { get; set; }

        public bool AccountHold { get; set; } = false;
        public string? GmtInfo { get; set; }
        public string? Imei { get; set; }
        public string? Username { get; set; }

        public Guid? TenantId { get; set; }

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        [StringLength(20)]
        public string AccountType { get; set; } = "single"; // single | multi

        // Navigation
        public Tenant? Tenant { get; set; }
        public Role Role { get; set; } = null!;
        public ICollection<Beacon> Beacons { get; set; } = new List<Beacon>();
        public ICollection<Gateway> Gateways { get; set; } = new List<Gateway>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<BeaconData> BeaconData { get; set; } = new List<BeaconData>();
        public ICollection<GatewayData> GatewayData { get; set; } = new List<GatewayData>();
        public ICollection<CustomerSupportTicket> SupportTickets { get; set; } = new List<CustomerSupportTicket>();
        public ICollection<DeviceToken> DeviceTokens { get; set; } = new List<DeviceToken>();
        public ICollection<EmailVerification> EmailVerifications { get; set; } = new List<EmailVerification>();
        public ICollection<Invite> SentInvites { get; set; } = new List<Invite>();
        public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
        public ICollection<UserPurchase> Purchases { get; set; } = new List<UserPurchase>();
        public ICollection<Setting> Settings { get; set; } = new List<Setting>();
        public ICollection<SnoozeSetting> SnoozeSettings { get; set; } = new List<SnoozeSetting>();
        public ICollection<Thing> Things { get; set; } = new List<Thing>();
        public ICollection<UserGateway> UserGateways { get; set; } = new List<UserGateway>();
        public ICollection<UserNotify> UserNotifies { get; set; } = new List<UserNotify>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}
