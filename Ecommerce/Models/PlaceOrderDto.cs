using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class PlaceOrderDto
    {
        public int UserId { get; set; }
        public BillingDetailsDto? BillingDetails { get; set; }
        public string PaymentMethod { get; set; } // "cod"
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal TotalAmount { get; set; }
        public string? CouponCode { get; set; }
        public int CouponID { get; set; }
        public int DeliveryAddress { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal DiscountAmount { get; set; }
        public decimal VatPercent { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal VatAmount { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        public decimal DeliveryCharge { get; set; }
        public string? PayInvoice { get; set; }
        public List<OrderItemDto> Items { get; set; }

        public string? FullName { get; set; }

        // ❌ No validation on Email
        public string? Email { get; set; }

        public string? Mobile { get; set; }

        public int? AreaId { get; set; }

        public int? BlockId { get; set; }

        public string? Street { get; set; }

        public string? HouseNo { get; set; }

        public string? Floor { get; set; }

        public string? ApartmentNo { get; set; }

        public int? ShareholderNo { get; set; }

        public string? Instruction { get; set; }

    }
}
