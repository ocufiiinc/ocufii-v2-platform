using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OcufiiAPI.Models
{
    public class Invite
    {
        [Key]
        public Guid InviteId { get; set; } = Guid.NewGuid();

        public Guid InvitingUserId { get; set; }

        [Required, EmailAddress, StringLength(255)]
        public string InvitedEmail { get; set; } = string.Empty;

        public int? RoleId { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? ExpiryTime { get; set; }

        public bool IsAccepted { get; set; } = false;

        public string? DeviceType { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User InvitingUser { get; set; } = null!;
        public Role? Role { get; set; }
    }
}
