namespace EcommerceWebApplication.Models
{
    public class ProductImageDto
    {
        public int ImageId { get; set; }
        public int ProductId { get; set; }
        public bool IsPrimary { get; set; }
        public string Base64 { get; set; }
    }
}
