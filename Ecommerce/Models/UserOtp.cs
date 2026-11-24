using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class UserOtp
    {
        [Key]
        public int OtpId { get; set; }
        public string? Email { get; set; }
        public string? Mobile { get; set; }
        public string Otp { get; set; }
        public DateTime Expiry { get; set; }
        public bool IsVerified { get; set; }
    }
}
