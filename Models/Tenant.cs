using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OcufiiAPI.Models
{
    public class Tenant
    {
        [Key]
        public Guid ResellerId { get; set; } = Guid.NewGuid();

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateUpdated { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "jsonb")]
        public string ThemeConfig { get; set; } = "{}";

        [Column(TypeName = "jsonb")]
        public string CustomWorkflows { get; set; } = "{}";

        // Navigation
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<FeatureFlag> FeatureFlags { get; set; } = new List<FeatureFlag>();
    }
}
