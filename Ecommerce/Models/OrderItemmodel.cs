using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class OrderItemmodel
    {
        [Key]
        public int OrderItemId { get; set; }
        public int ?  OrderId { get; set; }
        public int ? ProductId { get; set; }
        public int ? Quantity { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal? Price { get; set; }
    }
    public class OrderItemViewModel
    {
        
        public int OrderId { get; set; }
        public int ? OrderStatusId { get; set; }
        public int ? PaymentStatusId { get; set; }
        public string ?FullName { get; set; }
        public string ? ProductName { get; set; }
        public int  ? Quantity { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal ? Price { get; set; }
        public string? OrderStatus { get; set; }
        public string? PaymentStatus { get; set; }
        public string? Paytype { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime Orderdate { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal ? TotalPrice { get; set; }
    }
}
