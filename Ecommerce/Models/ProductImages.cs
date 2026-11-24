using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class ProductImages
    {
        [Key]   // ✅ Explicitly mark it as primary key

        public int ImageId { get; set; }
        public int ProductId { get; set; }

        public byte[] ImageData { get; set; }
        public string ContentType { get; set; }
        public bool? IsPrimary { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ✅ Navigation Property
        public virtual Products Product { get; set; }
    }

}
