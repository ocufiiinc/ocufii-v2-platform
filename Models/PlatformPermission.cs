namespace OcufiiAPI.Models
{
    public class PlatformPermission
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid AdminId { get; set; }
        public PlatformAdmin Admin { get; set; } = null!;

        public Guid PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;

        public bool IsGranted { get; set; } = false;

        public Guid GrantedByAdminId { get; set; }
        public PlatformAdmin GrantedByAdmin { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
