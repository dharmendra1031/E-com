using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class PageMaster
    {
        [Key]
        public int PageId { get; set; }
        public string ? PageNameEng { get; set; }
        public string ? PageNameArb { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
