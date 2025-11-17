using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class Thing
    {
        [Key]
        public Guid UserId { get; set; }

        [Key, StringLength(50)]
        public string GatewayMac { get; set; } = string.Empty;

        public string? Certificate { get; set; }
        public string? CertificateArn { get; set; }
        public string? CertificateId { get; set; }
        public string? PrivateKey { get; set; }
        public string? PublicKey { get; set; }

        public Gateway Gateway { get; set; } = null!;
    }
}
