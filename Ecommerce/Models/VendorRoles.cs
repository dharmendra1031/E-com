namespace EcommerceWebApplication.Models;

public static class VendorRoles
{
    public const string Owner = "VendorOwner";
    public const string Manager = "VendorManager";
    public const string Staff = "VendorStaff";

    public static bool IsVendorRole(string? name) => name is Owner or Manager or Staff;
}
