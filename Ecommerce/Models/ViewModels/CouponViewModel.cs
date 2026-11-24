using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels
{
    public class CouponViewModel
    {
        [Key]
        public int ? CouponId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ? Code { get; set; }

        [MaxLength(20)]
        public string? DiscountType { get; set; }
        public int? NoOfUses { get; set; }
        public decimal? DiscountValue { get; set; }

        public decimal? MinOrderAmount { get; set; }

        public decimal? MaxDiscountAmount { get; set; }
        public int? NoOfUsers { get; set; }

        public DateTime? ValidFrom { get; set; }

        public DateTime? ValidTo { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
