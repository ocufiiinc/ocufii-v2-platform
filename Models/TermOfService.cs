using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class TermOfService
    {
        [Key]
        public Guid TosId { get; set; } = Guid.NewGuid();

        [Required, StringLength(50)]
        public string Version { get; set; } = string.Empty;

        [Required]
        public string TermOfServiceText { get; set; } = string.Empty;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public Guid? CreatedBy { get; set; }

        public User? CreatedByUser { get; set; }
    }
}
