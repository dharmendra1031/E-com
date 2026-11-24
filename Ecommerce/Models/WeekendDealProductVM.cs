using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class WeekendDealProductVM
    {
        // Weekend Deal Fields
        [Key]
        public int? DealId { get; set; }
        public string? WdNameAr { get; set; }
        public string? WdNameEn { get; set; }
        public decimal? DiscPer { get; set; }
        public DateTime  FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // Product Fields
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductNameAr { get; set; }
        public decimal ProductPrice { get; set; }
        public int Stock { get; set; }

        // Calculated Fields
        public decimal? DiscountAmount { get; set; }
        public decimal OfferPrice { get; set; }

        // Multiple Images
        public List<string> ProductImagesBase64 { get; set; }
    }
}
