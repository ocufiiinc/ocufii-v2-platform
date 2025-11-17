using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class Permission
    {
        [Key]
        public Guid PermissionId { get; set; } = Guid.NewGuid();

        public int RoleId { get; set; }

        [Required, StringLength(100)]
        public string Resource { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Action { get; set; } = string.Empty;

        [StringLength(50)]
        public string Scope { get; set; } = "tenant"; // global, tenant, resource

        public Role Role { get; set; } = null!;
    }
}
