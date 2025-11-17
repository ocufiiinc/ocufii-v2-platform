using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class Setting
    {
        [Key]
        public Guid UserId { get; set; }

        public bool? ActiveShooter { get; set; }
        public bool? AutoLogout { get; set; }
        public int? AutoLogoutInterval { get; set; }
        public bool? BypassFocus { get; set; }
        public bool? Distress { get; set; }
        public bool? Emergency { get; set; }
        public bool? Emergency911 { get; set; }
        public bool? MovementSound { get; set; }
        public bool? MovementVibration { get; set; }
        public bool? PersonalSafety { get; set; }

        [StringLength(100)]
        public string? PersonalSafetyUserName { get; set; }

        public bool? Sound { get; set; }
        public Guid? TosId { get; set; }

        [StringLength(50)]
        public string? TosVersion { get; set; }

        public User User { get; set; } = null!;
        public TermOfService? TermOfService { get; set; }
    }
}
