using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class DeviceToken
    {
        [Key]
        public Guid UserId { get; set; }

        [StringLength(255)]
        public string DeviceTokenValue { get; set; } = string.Empty;

        [StringLength(100)]
        public string? MobileDevice { get; set; }

        [StringLength(50)]
        public string? MobileOsVersion { get; set; }

        [StringLength(50)]
        public string? Version { get; set; }

        public User User { get; set; } = null!;
    }
}
