using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class PageMaster
    {
        [Key]
        public int PageId { get; set; }
        public string ? PageNameEng { get; set; }
        public string ? PageNameArb { get; set; }

        [MaxLength(80)] public string? PageCode { get; set; }
        [Required, MaxLength(10)] public string PortalScope { get; set; } = "Admin";

        public DateTime CreatedAt { get; set; }
    }
}
