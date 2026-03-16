namespace OcufiiAPI.Models
{
    public class MembershipPermission
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }  
        public User User { get; set; } = null!; 

        public Guid PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;

        public bool IsGranted { get; set; } = false;

        public Guid GrantedByUserId { get; set; }
        public User GrantedByUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
