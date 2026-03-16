namespace OcufiiAPI.Models
{
    public class RolePermission
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public Guid PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;

        public bool IsGranted { get; set; } = false;
        public bool IsSystemCeiling { get; set; } = false;

        public Guid? UpdatedByAdminId { get; set; }
        public PlatformAdmin? UpdatedByAdmin { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
