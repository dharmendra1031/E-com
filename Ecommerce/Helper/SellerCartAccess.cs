using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Helper;

public static class SellerCartAccess
{
    public static bool Enabled(IConfiguration configuration) => MarketplaceOffers.Enabled(configuration) &&
        configuration.GetValue("Marketplace:SellerCartEnabled", false);

    public static bool CheckoutEnabled(IConfiguration configuration, IWebHostEnvironment environment) =>
        environment.IsDevelopment() && Enabled(configuration) &&
        configuration.GetValue("Marketplace:SellerCheckoutTestEnabled", false);

    public static int? CustomerId(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("Customer") &&
        int.TryParse(context.User.FindFirst("UserId")?.Value,out var id) && id>0 &&
        context.Session.GetInt32("UserId")==id ? id : null;

    public static async Task<int?> CustomerAsync(EcommerceDbContext db, HttpContext context, CancellationToken ct)
    {
        if (CustomerId(context) is not int id) return null;
        return await db.Customers.AnyAsync(c => c.UserId == id && c.IsActive && c.IsDeleted == 0 && c.CustType == "Customer" &&
            c.Role != null && c.Role.IsActive && c.Role.RoleName == "Customer", ct) ? id : null;
    }

    public static IQueryable<CartItem> Lines(EcommerceDbContext db, int customerId) =>
        db.CartItems.IgnoreQueryFilters().Where(c => c.UserId == customerId && c.VendorListingId != null);

    public static async Task<List<SellerCartLine>> ReadAsync(EcommerceDbContext db, int customerId, CancellationToken ct)
    {
        var rows = await (from c in Lines(db, customerId).AsNoTracking()
            join l in db.VendorListings.AsNoTracking() on c.VendorListingId equals (int?)l.VendorListingId
            select new SellerCartLine
            {
                CartItemId = c.CartItemId, ListingId = l.VendorListingId, VendorId = l.VendorId,
                VendorName = l.Vendor.Name, VendorNameArb = l.Vendor.NameArb,
                ProductId = l.ProductId, ProductName = l.Product.Name, ProductNameArb = l.Product.NameArb,
                Quantity = c.Quantity, SavedPrice = c.Price
            }).OrderBy(c => c.VendorId).ThenBy(c => c.CartItemId).ToListAsync(ct);
        var ids = rows.Select(c => c.ListingId).ToArray();
        if (ids.Length == 0) return rows;
        var offers = await MarketplaceOffers.Available(db).Where(o => ids.Contains(o.ListingId))
            .ToDictionaryAsync(o => o.ListingId, ct);
        foreach (var row in rows)
            if (offers.TryGetValue(row.ListingId, out var offer))
            {
                row.CurrentPrice = offer.Price;
                row.AvailableQuantity = offer.AvailableQuantity;
            }
        return rows;
    }
}
