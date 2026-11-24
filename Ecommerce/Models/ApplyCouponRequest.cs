namespace EcommerceWebApplication.Models
{
    public class ApplyCouponRequest
    {
        public string Code { get; set; }
        public int UserId { get; set; }
        public decimal OrderAmount { get; set; }
    }
}
