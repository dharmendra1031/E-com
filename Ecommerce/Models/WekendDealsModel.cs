using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class WekendDealsModel
    {
        [Key]
        public int WdId { get; set; }

        public string WdNameAr { get; set; }
        public string WdNameEn { get; set; }

        public int ProductId { get; set; }

        public decimal DiscPer { get; set; }
        [NotMapped]
        public string FromDateStr { get; set; }
        [NotMapped]
        public string ToDateStr { get; set; }

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public int? MaxQuantity { get; set; }   // NULL allowed → int?
        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

    }
}
