using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    [Table("Customers")] // ✅ Matches your table name
    public class Customers
    {
        [Key]
        public int UserId { get; set; }

        [Required, MaxLength(100)]
        public string? FullName { get; set; }

        [MaxLength(100)]
        public string? FullNameArb { get; set; }

        [Required, MaxLength(150)]
        [EmailAddress]
        public string? Email { get; set; }
        public int? IsDeleted { get; set; }

        [MaxLength(10)]
        [RegularExpression(@"^\+?[0-9]*$", ErrorMessage = "Phone number can only contain digits and an optional '+' at the beginning.")]
        public string? Phone { get; set; }
        public string? CustType { get; set; }
        public int? ShareholderNo { get; set; }
        //public string Phone { get; set; }

        public string? PasswordHash { get; set; }

        // ✅ Foreign Key → Role
        public int RoleId { get; set; }
        [ForeignKey("RoleId")]
        public Role? Role { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? UpdatedBy { get; set; }
        public byte[]? ProfileImage { get; set; }

        public DateTime? BirthDate { get; set; }


    }
}
