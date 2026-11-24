using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class Coupon
    {
        [Key]
        public int CouponId { get; set; }

        [Required]
        [MaxLength(50)]
        public string? Code { get; set; }

        [MaxLength(20)]
        public string DiscountType { get; set; }  // "Percentage" or "Fixed"
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal? DiscountValue { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal? MinOrderAmount { get; set; }
        public int ? NoOfUsers { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal MaxDiscountAmount { get; set; }

        public DateTime?  ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }

        [NotMapped]
        public string  ValidFromm { get; set; }
        [NotMapped]
        public string  ValidToo { get; set; }

        public bool IsActive { get; set; } = true;
    }

}
