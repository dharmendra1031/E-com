using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class OrderDetailModel
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal TotalAmount { get; set; }

        public string ? Status { get; set; }
        public string ? PaymentStatus { get; set; }
        public string? PaymentType { get; set; }

        public string? CouponCode { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal? DiscountAmount { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? FullName { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal? Price { get; set; }
        public decimal? Vatpersent { get; set; }
        public int? ShareholderNo { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal? VatAmount { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        public decimal? DeliveryCharge { get; set; }

        public string? House { get; set; }
        public string? Street { get; set; }
        public string? Block { get; set; }
        public string? Area { get; set; }
        public string? Governorate { get; set; }
        public string? Landmark { get; set; }
        public string? Floor { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public List<OrderItemDetailViewModel> Items { get; set; } = new();
    }

    public class OrderItemDetailViewModel
    {
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal Price { get; set; }
    }
}
