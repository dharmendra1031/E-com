using System.ComponentModel.DataAnnotations;
namespace EcommerceWebApplication.Models
{
    public class TopCategory
    {
        [Key]
        public int TopCategoryId { get; set; }
        [Required]
        public int CategoryId { get; set; }
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(200)]
        public string TitleArbic { get; set; }

        [StringLength(300)]
        public string SubTitle { get; set; }

        [StringLength(300)]
        public string SubTitleArbic { get; set; }

        public byte[] ? TopCategoryImage { get; set; }

        public DateTime? CreatedAt { get; set; }

        public int? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? UpdatedBy { get; set; }
    }
}
