using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class SpecificationModel
    {
        [Key]
        public int Id { get; set; }

        public string? SpeciNameAr { get; set; }
        public string? SpecivalueAr { get; set; }

        public string? SpeciNameEn { get; set; }
        public string? SpecivalueEn { get; set; }

        public int? ProductId { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
