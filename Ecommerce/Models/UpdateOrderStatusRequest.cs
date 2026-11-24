namespace EcommerceWebApplication.Models
{
    public class UpdateOrderStatusRequest
    {
        public int OrderId { get; set; }
        public string? Transid { get; set; } = "0";
    }
}
