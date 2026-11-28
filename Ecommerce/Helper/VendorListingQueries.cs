using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Helper;

public static class VendorListingQueries
{
    public static async Task<VendorListingsPage> ReadPageAsync(EcommerceDbContext db, int vendorId,
        string name, string? search, int page, bool canEdit, bool isAdmin, CancellationToken ct, string? filter = null)
    {
        const int size = 25;
        page = Math.Clamp(page, 1, 100000);
        search = search?.Trim();
        var query = db.VendorListings.AsNoTracking().Where(l => l.VendorId == vendorId);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(l => l.SellerSku.Contains(search) || l.Product.Name.Contains(search) ||
                l.Product.NameArb.Contains(search));
        if (filter == "Approved")
            query = query.Where(l => l.Status == "Approved" && l.Product.IsActive);
        else if (filter is "Pending" or "Inactive")
            query = query.Where(l => l.Status == filter);
        else if (filter == "OutOfStock")
        {
            var outOfStock = db.Database.SqlQuery<int>($"""
                SELECT l.VendorListingId AS Value FROM dbo.VendorListings l
                LEFT JOIN dbo.InventoryBalances b ON b.VendorListingId=l.VendorListingId
                WHERE l.VendorId={vendorId} AND COALESCE(b.AvailableQuantity,0)=0
                """);
            query = query.Where(l => l.Status == "Approved" && l.Product.IsActive && outOfStock.Contains(l.VendorListingId));
        }
        var rows = await query.OrderBy(l => l.VendorListingId).Skip((page - 1) * size).Take(size + 1)
            .Select(l => new VendorListingsPage.Row
            {
                ListingId = l.VendorListingId, ProductId = l.ProductId, ProductName = l.Product.Name,
                ProductNameArb = l.Product.NameArb, SellerSku = l.SellerSku, Price = l.Price,
                Status = l.Status, ProductActive = l.Product.IsActive, RowVersion = l.RowVersion
            }).ToListAsync(ct);
        return new VendorListingsPage { VendorId = vendorId, VendorName = name, Search = search, Filter = filter,
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
