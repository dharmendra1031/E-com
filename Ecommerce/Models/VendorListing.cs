using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models;

[Table("VendorListings")]
public class VendorListing
{
    [Key] public int VendorListingId { get; set; }
    public int VendorId { get; set; }
    public int ProductId { get; set; }
    [Required, MaxLength(100)] public string SellerSku { get; set; } = "";
    [Column(TypeName = "decimal(18,3)")] public decimal Price { get; set; }
    [Required, MaxLength(12), Column(TypeName = "varchar(12)")] public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = null!;
    public Products Product { get; set; } = null!;
    public Vendor Vendor { get; set; } = null!;
}
