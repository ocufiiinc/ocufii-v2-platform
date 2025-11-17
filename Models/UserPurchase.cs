using System.ComponentModel.DataAnnotations;

namespace OcufiiAPI.Models
{
    public class UserPurchase
    {
        [Key, StringLength(255)]
        public string PurchaseToken { get; set; } = string.Empty;

        public Guid UserId { get; set; }

        [StringLength(10)]
        public string OsType { get; set; } = string.Empty; // android, ios

        [StringLength(255)]
        public string? TransactionId { get; set; }

        public User User { get; set; } = null!;
    }
}
