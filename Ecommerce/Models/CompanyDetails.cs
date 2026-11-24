using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class CompanyDetails
    {
        [Key]
        public int CompanyId { get; set; }

        [MaxLength(200)]
        public string CompanyName { get; set; }

        [MaxLength(200)]
        public string CompanyNameArabic { get; set; }

        [MaxLength(300)]
        public string Address { get; set; }

        [MaxLength(300)]
        public string AddressArabic { get; set; }

        [MaxLength(100)]
        public string Email { get; set; }

        [MaxLength(15)]
        public string Phone { get; set; }

        public byte[]? Logo { get; set; }

        [MaxLength(100)]
        public string FacebookUrl { get; set; }

        [MaxLength(100)]
        public string TwitterUrl { get; set; }

        [MaxLength(100)]
        public string InstagramUrl { get; set; }

        [MaxLength(100)]
        public string  LinkedInUrl { get; set; }

        public decimal Vat { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public int? CreatedBy { get; set; }
        public bool? Heading { get; set; } = true;
        public int? UpdatedBy { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        public decimal DeliveryCharge { get; set; }
        [Column("QtyLimit")]
        public int QuantityLimit { get; set; }
    }
    public class HomeViewModel
    {
        public CompanyDetails CompanyDetails { get; set; }
        public List<WeekendDealProductVM> WeekendDeals { get; set; }


    }
}
