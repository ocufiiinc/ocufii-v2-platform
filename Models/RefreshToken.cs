using System.ComponentModel.DataAnnotations.Schema;

namespace OcufiiAPI.Models
{
    public class RefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public void Revoke()
        {
            IsActive = false;
        }

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
}
