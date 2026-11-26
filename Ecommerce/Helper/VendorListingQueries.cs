using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Helper;

public static class VendorListingQueries
{
    public static async Task<VendorListingsPage> ReadPageAsync(EcommerceDbContext db, int vendorId,
        string name, string? search, int page, bool canEdit, bool isAdmin, CancellationToken ct)
    {
        const int size = 25;
        page = Math.Clamp(page, 1, 100000);
        search = search?.Trim();
        var query = db.VendorListings.AsNoTracking().Where(l => l.VendorId == vendorId);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(l => l.SellerSku.Contains(search) || l.Product.Name.Contains(search) ||
                l.Product.NameArb.Contains(search));
        var rows = await query.OrderBy(l => l.VendorListingId).Skip((page - 1) * size).Take(size + 1)
            .Select(l => new VendorListingsPage.Row
            {
                ListingId = l.VendorListingId, ProductId = l.ProductId, ProductName = l.Product.Name,
                ProductNameArb = l.Product.NameArb, SellerSku = l.SellerSku, Price = l.Price,
                Status = l.Status, ProductActive = l.Product.IsActive, RowVersion = l.RowVersion
            }).ToListAsync(ct);
        return new VendorListingsPage { VendorId = vendorId, VendorName = name, Search = search,
            Page = page, CanEdit = canEdit, IsAdmin = isAdmin, HasNext = rows.Count > size,
            Rows = rows.Take(size).ToList() };
    }

    public static bool TryVersion(string? value, out byte[] version)
    {
        version = Array.Empty<byte>();
        if (string.IsNullOrEmpty(value) || value.Length != 12) return false;
        try { version = Convert.FromBase64String(value); return version.Length == 8; }
        catch (FormatException) { return false; }
    }
}
