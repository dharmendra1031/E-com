using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels
{
    public class WekendDealsViewModel
    {
        [Key]
        public int WdId { get; set; }

        public string? WdNameAr { get; set; }
        public string? WdNameEn { get; set; }

        public int? ProductId { get; set; }

        public decimal? DiscPer { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public int? MaxQuantity { get; set; }   // NULL allowed → int?

        public int? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }

        public int? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
