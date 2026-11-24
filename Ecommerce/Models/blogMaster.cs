using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{

    public class blogMaster
    {
        [Key]
        public int BlogId { get; set; }

        [Required(ErrorMessage = "English Title is required")]
        [MaxLength(200)]
        public string? Title { get; set; }
        [Required(ErrorMessage = "Arbic Title is required")]
        [MaxLength(200)]
        public string? TitleArbic { get; set; }

        [Required(ErrorMessage = "English Description is required")]
        public string? DescriptionEng { get; set; }
        [Required(ErrorMessage = "Arbic Description is required")]
        public string? DescriptionArb { get; set; }

        public byte[]? BlogImage { get; set; }

        [Required]
        [Range(0, 1, ErrorMessage = "Invalid status")]
        public int IsActive { get; set; } = 1;

        public DateTime? CreatedAt { get; set; }

        public int? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? UpdatedBy { get; set; }

    }
}
