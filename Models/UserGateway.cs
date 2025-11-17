using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class UserGateway
    {
        [Key]
        public Guid UserId { get; set; }

        [Key, StringLength(50)]
        public string GatewayMac { get; set; } = string.Empty;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime DateAssociated { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
        public Gateway Gateway { get; set; } = null!;
    }
}
