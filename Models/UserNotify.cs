using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class UserNotify
    {
        [Key]
        public Guid UserNotifyId { get; set; } = Guid.NewGuid();

        public Guid? UserId { get; set; }

        [StringLength(20)]
        public string? Otp { get; set; }

        public string? CustomMessage { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? ExpirationTime { get; set; }

        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        public bool? ReceiveAlert { get; set; }

        [StringLength(100)]
        public string? RecipientName { get; set; }

        [StringLength(100)]
        public string? SenderName { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [StringLength(255)]
        public string? DeviceToken { get; set; }

        public bool? EnableLocation { get; set; }
        public bool? EnableRecipient { get; set; }

        [StringLength(100)]
        public string? MobileDevice { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? SnoozeStartTime { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? SnoozeEndTime { get; set; }

        [StringLength(50)]
        public string? UserStatus { get; set; }

        public User? User { get; set; }
    }
}
