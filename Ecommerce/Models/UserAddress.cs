using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class UserAddress
    {
        [Key]
        public int AddressId { get; set; }

        // ✅ UserId should be INT (foreign key to Users)
        public int UserId { get; set; }

        public string? Title { get; set; }        
        public string? Area { get; set; }
        public string? Block { get; set; }
        public string? Street { get; set; }
        public string? HouseNo { get; set; }
        public string? Floor { get; set; }
        public string? Landmark { get; set; }
        public string? Governorate { get; set; }
       

        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Country { get; set; }
        public string? Phone { get; set; }
        public int? isdeleted { get; set; }

        public bool IsDefault { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ✅ Navigation property
        public virtual User? User { get; set; }
    }

}
