using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    [Table("CartItems")]
    public class CartItem
    {
        [Key]
        public int CartItemId { get; set; }

        [Required]
        public int? UserId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [MaxLength(255)]
        public string ProductName { get; set; }

        [MaxLength(255)]
        public string ProductNameArb { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        [DisplayFormat(DataFormatString = "{0:F3}", ApplyFormatInEditMode = true)]
        public decimal Price { get; set; }

        [MaxLength(100)]
        public string DeviceId { get; set; }   // Guest cart key

        public int Quantity { get; set; } = 1;

        public string ImageBase64 { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
