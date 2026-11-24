using static EcommerceWebApplication.Controllers.EcommerceApiController;

namespace EcommerceWebApplication.Models
{
    public class ProductDetailsDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string NameArb { get; set; }
        public string Description { get; set; }
        public string BrandName { get; set; }
        public string SKUId { get; set; }
        public int Units { get; set; }
        public string ProductsModel { get; set; }
        public string Warranty { get; set; }
        public string DescriptionArb { get; set; }
        public decimal Price { get; set; }
        public bool IsInCart { get; set; }
        public decimal Stock { get; set; }
        public decimal Weight { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public List<ProductImageDto> Images { get; set; } = new();
        public List<ProductSpecificationDto> Specifications { get; set; }

    }

}
