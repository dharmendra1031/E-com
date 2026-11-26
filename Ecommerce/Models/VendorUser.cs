using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models;

[Table("VendorUsers")]
public class VendorUser
{
    public int VendorId { get; set; }
    public int UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public Vendor Vendor { get; set; } = null!;
    public User User { get; set; } = null!;
}
