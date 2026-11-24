namespace EcommerceWebApplication.Models
{
    public class CartItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductNameArb { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageBase64 { get; set; }
    }
}
