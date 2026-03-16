namespace OcufiiAPI.Models
{
    public class ResellerPermission
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ResellerId { get; set; }
        public Reseller Reseller { get; set; } = null!;

        public Guid PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;

        public bool IsGranted { get; set; } = false;

        public Guid GrantedByAdminId { get; set; }
        public PlatformAdmin GrantedByAdmin { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
