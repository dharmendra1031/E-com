using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{

    public class BrandModel
    {
        [Key]
        public int BrandId { get; set; }
        public string BrandNameAr { get; set; }
        public string BrandNameAEn { get; set; }
        public byte[]? BrandLogo { get; set; }
        public decimal? DiscountPersent { get; set; }
        public string? Discriptionar { get; set; }
        public string? Discriptionen { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? Createdby { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? Updatedby { get; set; }
    }

}
