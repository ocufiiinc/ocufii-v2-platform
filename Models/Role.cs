using System.ComponentModel.DataAnnotations;
using System.Security;

namespace OcufiiAPI.Models
{
    public class Role
    {
        [Key]
        public int RoleId { get; set; }

        [Required, StringLength(50)]
        public string RoleName { get; set; } = string.Empty;

        public string? RoleDescription { get; set; }

        [Required, StringLength(50)]
        public string PermissionLevel { get; set; } = string.Empty;

        // Navigation
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
