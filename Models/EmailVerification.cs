using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class EmailVerification
    {
        [Key]
        public Guid UserId { get; set; }

        [Key, StringLength(255)]
        public string Token { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Category { get; set; }

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? ExpirationTime { get; set; }

        public bool IsVerified { get; set; } = false;

        public User User { get; set; } = null!;
    }
}
