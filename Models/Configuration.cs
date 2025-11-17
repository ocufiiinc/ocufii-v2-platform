using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class Configuration
    {
        [Key, StringLength(100)]
        public string ConfigurationId { get; set; } = string.Empty;

        [Key, StringLength(50)]
        public string ModeType { get; set; } = string.Empty;

        [Column(TypeName = "jsonb")]
        [Required]
        public string ConfigurationData { get; set; } = string.Empty;
    }
}
