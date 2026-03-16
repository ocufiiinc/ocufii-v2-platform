using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class Permission
    {
        public Guid PermissionId { get; set; } = Guid.NewGuid();
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty; 
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = "account";
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
