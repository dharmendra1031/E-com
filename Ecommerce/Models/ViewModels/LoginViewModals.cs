using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels
{
    public class LoginViewModals
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        public string? ReturnUrl { get; set; }
        [ValidateNever]
        public List<CompanyDetails> CompanyDetails { get; set; }
    }
}
