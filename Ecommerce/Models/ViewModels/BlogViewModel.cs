using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models.ViewModels
{
   
    public class BlogViewModel
    {
        public int BlogId { get; set; }
        public string? Title { get; set; }
        public string? DescriptionEng { get; set; }
        public int ? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        [NotMapped]
        public string? BlogImageBase64 { get; set; } // For display only
    }
}
