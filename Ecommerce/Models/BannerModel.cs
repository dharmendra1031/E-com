using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    [Table("Banner")]
    public class BannerModel
    {
        [Key]
        public int BannerId { get; set; }
        public string? Title { get; set; }
        public string? TitleArabic { get; set; }
         public byte[]? BannerImage { get; set; }
        public int? DisplayOrder { get; set; } = 0;

        [NotMapped]
        public string BannerImageBase64 => BannerImage != null
        ? $"data:image/jpeg;base64,{Convert.ToBase64String(BannerImage)}"
        : null;
    }
}
