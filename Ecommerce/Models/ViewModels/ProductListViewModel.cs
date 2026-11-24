namespace EcommerceWebApplication.Models.ViewModels
{
    public class ProductListViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string NameArb { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string CategoryName { get; set; }
        public string SubCategoryName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Rating { get; set; }
        public double? AverageRating { get; set; }
        public int? ReviewCount { get; set; }
        // ✅ Primary image as Base64 for display
        public string ProductImageBase64 { get; set; }
        public int? ProductImageId { get; set; }
    }

}
