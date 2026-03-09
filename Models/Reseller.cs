namespace OcufiiAPI.Models
{
    public class Reseller
    {
        public Guid ResellerId { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty; 
        public string? Email { get; set; }
        public string? ContactName { get; set; }
        public string? PhoneNumber { get; set; }
        public Guid? CreatedByAdminId { get; set; }
        public PlatformAdmin? CreatedByAdmin { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "reseller_admin";
    }
}
