using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class Area
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Area Name is required")]
        public string AreaName { get; set; }

        public string AreaNameArb { get; set; }

        public virtual ICollection<Block> Blocks { get; set; } = new List<Block>();

    }
}
