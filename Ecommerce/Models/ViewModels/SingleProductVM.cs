namespace EcommerceWebApplication.Models.ViewModels
{
    public class SingleProductVM
    {
        // ===== Basic Product Info =====
        public int ProductId { get; set; }
        public string? Name { get; set; }
        public string? NameArb { get; set; }
        public string? Description { get; set; }
        public string? DescriptionArb { get; set; }

        // ===== Category Info (Extra) =====
        public int? CategoryId { get; set; }
        public int? SubCategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? SubCategoryName { get; set; }

        // ===== Brand =====
        public int? BrandId { get; set; }
        public SingleBrandDto? Brand { get; set; }

        // ===== SKU & Identifiers =====
        public string? SKUId { get; set; }
        public string? Ds_Barcode { get; set; }
        public string? Ds_ProdID { get; set; }

        // ===== Pricing =====
        public decimal? Price { get; set; }
        public decimal? CoastPrice { get; set; }
       
        // ===== Stock & Units =====
        public decimal? Stock { get; set; }
        public int? Units { get; set; } = 1;
        public decimal? Weight { get; set; }

        // ===== Product Details =====
        public string? ProductsModel { get; set; }
        public string? Warranty { get; set; }

        // ===== Status =====
        public bool IsActive { get; set; }

        // ===== Rating & Reviews =====
        public double AverageRating { get; set; }
        public int? TotalReviews { get; set; }

        public List<SingleReviewDto> Reviews { get; set; } = new();
        public List<SigleImageDto> Images { get; set; } = new();

        // ===== UI Helper (Extra) =====
        public bool IsInStock => Stock.HasValue && Stock > 0;

        public List<SingleSpecificationDto> Specifications { get; set; } = new();
        public decimal? OfferPrice { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? DiscPer { get; set; }
        public bool IsWeekendDeal { get; set; }

    }
}
