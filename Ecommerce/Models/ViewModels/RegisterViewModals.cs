using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels
{
    public class RegisterViewModals
    {
        [Required(ErrorMessage = "Full name is required")]
        public string FullName { get; set; }  // First + Last combined

        [Required(ErrorMessage = "Email is required")]
        [RegularExpression(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        ErrorMessage = "Invalid email format"
        )]
        public string Email { get; set; }

        [DataType(DataType.Date)]
        [Required(ErrorMessage = "Birth date is required")]
        public DateTime BirthDate { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [StringLength(50, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long")]
        public string Password { get; set; }

        [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }



        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^(\+965)?[569]\d{7}$",
          ErrorMessage = "Invalid Kuwaiti phone number")]
        public string Phone { get; set; }
        public int Isdeleted { get; set; }
        public string? ReturnUrl { get; set; }

        // Backend automatically assigns
        public int RoleId { get; set; } = 2; // default Customer role
        public bool IsActive { get; set; } = true;
        [ValidateNever]
        public List<CompanyDetails> ? CompanyDetails { get; set; }
    }
}
