namespace EcommerceWebApplication.Models
{
    public class ProductSuggestionDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string NameArb { get; set; }
        public decimal? Price { get; set; }
        public string Base64Image { get; set; } // ✅ Instead of URL
    }
}
