namespace EcommerceWebApplication.Models
{

    public class LatestOrdersViewModel
    {
        public List<LatestOrderItem> Orders { get; set; }
        public int NewOrdersCount { get; set; }
        public int LastOrderId { get; set; }
    }

    public class LatestOrderItem
    {
        public int OrderId { get; set; }
        public string PaymentType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
