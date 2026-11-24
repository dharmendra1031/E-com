using System.ComponentModel.DataAnnotations;
namespace EcommerceWebApplication.Models.ViewModels
{
    public class ChangePasswordModel
    {
        public int UserId { get; set; }

        [Required]
        public string NewPassword { get; set; }
    }
}
