using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OcufiiAPI.Models
{
    public class Tenant
    {
        [Key]
        public Guid TenantId { get; set; } = Guid.NewGuid();

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateUpdated { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "jsonb")]
        public string ThemeConfig { get; set; } = "{}";

        [Column(TypeName = "jsonb")]
        public string CustomWorkflows { get; set; } = "{}";
        public bool IsActive { get; set; } = true;

        public Guid? AssignedResellerId { get; set; } = new Guid("00000000-0000-0000-0000-000000000001");
        public Reseller AssignedReseller { get; set; } = null!;

        // Navigation
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<FeatureFlag> FeatureFlags { get; set; } = new List<FeatureFlag>();
    }
}
