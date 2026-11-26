using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class OrderModel
    {
        [Key]
        public int OrderId { get; set; }

        public int CustomerId { get; set; }
        public Guid? MarketplaceRequestId { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal VatAmount { get; set; }

        public decimal VatPercent { get; set; }
        [ForeignKey("OrderStatus")]
        public int Status { get; set; }
        public virtual OrderStatus OrderStatus { get; set; }

        public int PaymentStatus { get; set; }

        public string? PaymentType { get; set; }

        public string? CouponCode { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal? DiscountAmount { get; set; }

        public virtual ReturnRequest ReturnRequest { get; set; }

        public int? DeliveryAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string OrderTransaction { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        public decimal DeliveryCharge { get; set; }

        public int NewNotification { get; set; }
        

    }
}
