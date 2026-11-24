using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class AboutUs
    {
        [Key]
        public int AboutUsId { get; set; }

        [Required(ErrorMessage = "Title (English) is required")]
        [MaxLength(200)]
        public string? TitleEng { get; set; }

        [Required(ErrorMessage = "Subtitle (Arabic) is required")]
        [MaxLength(300)]
        public string? SubTitleArb { get; set; }

        [Required(ErrorMessage = "Title (Arabic) is required")]
        [MaxLength(200)]
        public string? TitleArb { get; set; }

        [Required(ErrorMessage = "Subtitle (English) is required")]
        [MaxLength(300)]
        public string? SubTitleEng { get; set; }

        [Required(ErrorMessage = "Description (English) is required")]
        public string? DescriptionEng { get; set; }

        [Required(ErrorMessage = "Description (Arabic) is required")]
        public string? DescriptionArb { get; set; }
  

        public byte[]? AboutImage { get; set; }

        public int IsActive { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? UpdatedBy { get; set; }
    }

}
