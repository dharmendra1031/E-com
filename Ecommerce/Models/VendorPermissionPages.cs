namespace EcommerceWebApplication.Models;

public static class VendorPermissionPages
{
    public const string Dashboard = "vendor.dashboard";
    public const string Listings = "vendor.listings";
    public const string Stock = "vendor.stock";
    public const string Orders = "vendor.orders";
    public const string Finance = "vendor.finance";
    public static string ScopeFor(string? role) => VendorRoles.IsVendorRole(role) ? "Vendor" : "Admin";

    public static bool SupportsCreate(string? code) => code == Listings;
    public static bool SupportsUpdate(string? code) => code is Listings or Stock or Orders;
    public static bool IsKnown(string? code) => code is Dashboard or Listings or Stock or Orders or Finance;
}
