using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels;

public class VendorForm
{
    public int VendorId { get; set; }
    [Required, StringLength(50)]
    public string VendorCode { get; set; } = "";
    [Required, StringLength(200)]
    public string Name { get; set; } = "";
    [StringLength(200)]
    public string? NameArb { get; set; }
}
