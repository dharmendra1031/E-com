namespace EcommerceWebApplication.Models
{
    public class OrderprofileViewModel
    {
        public int? OrderId { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? OrderStatusName { get; set; }
        public string? PaymentStatusName { get; set; }
        public string? paytype { get; set; }

        public decimal TotalAmount { get; set; }
    }
}
