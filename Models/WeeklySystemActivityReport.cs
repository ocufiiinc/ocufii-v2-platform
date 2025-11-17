using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class WeeklySystemActivityReport
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? SentDate { get; set; }
    }
}
