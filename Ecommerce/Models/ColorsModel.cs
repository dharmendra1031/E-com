using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class ColorsModel
    {
        [Key]
        public int Id { get; set; }

        public string Colorcode { get; set; }
    }
}
