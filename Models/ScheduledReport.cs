using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class ScheduledReport
    {
        [Key]
        public Guid ScheduledReportId { get; set; }

        [Key, StringLength(50)]
        public string ReportType { get; set; } = string.Empty;

        [Column(TypeName = "timestamp with time zone")]
        public DateTime? DateScheduled { get; set; }
    }
}
