using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class Category
    {
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Category Name is required")]
        [StringLength(100)]
        public string CName { get; set; }

        [Required(ErrorMessage = "Arabic Name is required")]
        [StringLength(100)]
        public string CNameArb { get; set; }

        public string? CDescription { get; set; }
        public string? CDescriptionArbic { get; set; }

        public byte[] CategoryImage { get; set; }
        public byte[]? CategoryIcon { get; set; }
        // will store image path or base64
        [NotMapped]
        public string ImageBase64 { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public ICollection<SubCategory> SubCategories { get; set; }
    }
}
