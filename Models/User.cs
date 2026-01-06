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
        public ICollection<Device> Devices { get; set; } = new List<Device>();
        public Tenant? Tenant { get; set; }
        public Role Role { get; set; } = null!;
        public Guid? ParentId { get; set; }
        public User? Parent { get; set; }
        public ICollection<User> Dependents { get; set; } = new List<User>();
        public ICollection<UserFeature> UserFeatures { get; set; } = new List<UserFeature>();
        public ICollection<DeviceToken> DeviceTokens { get; set; } = new List<DeviceToken>();       
    }
}
