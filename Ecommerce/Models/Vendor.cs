using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models;

[Table("Vendors")]
public class Vendor
{
    [Key]
    public int VendorId { get; set; }
    [Required, MaxLength(50)]
    public string VendorCode { get; set; } = "";
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";
    [MaxLength(200)]
    public string? NameArb { get; set; }
    [Required, MaxLength(12), Column(TypeName = "varchar(12)")]
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; }
}
