using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OcufiiAPI.Models
{
    public class FeatureFlag
    {
        [Key]
        public Guid FlagId { get; set; } = Guid.NewGuid();

        public Guid? TenantId { get; set; }

        [Required, StringLength(100)]
        public string FlagName { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        [Column(TypeName = "jsonb")]
        public string Config { get; set; } = "{}";

        [Column(TypeName = "timestamp with time zone")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Tenant? Tenant { get; set; }
    }
}
