using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class CouponUsage
    {
        [Key]
        public int UsageId { get; set; }  // Primary key

        public int CouponId { get; set; }  // FK -> Coupons
        public int UserId { get; set; }    // FK -> Users

        public DateTime UsedAt { get; set; } = DateTime.UtcNow;

        // ✅ Navigation properties
        public Coupon Coupon { get; set; }
        public User User { get; set; }
    }

}
