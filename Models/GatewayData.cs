using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class GatewayData
    {
        [Key]
        public Guid UserId { get; set; }

        [Key]
        [StringLength(50)]
        public string GatewayMac { get; set; } = string.Empty;

        [Key]
        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateUpdated { get; set; }

        [Column(TypeName = "jsonb")]
        public string? Data { get; set; }

        [StringLength(50)]
        public string? GatewayStatus { get; set; }

        public Gateway Gateway { get; set; } = null!;
    }
}
