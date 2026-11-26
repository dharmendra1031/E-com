using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels;

public class VendorLoginForm
{
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = "";
    [Required, StringLength(128), DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public class VendorPortalStore
{
    public int VendorId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? NameArb { get; set; }
}
