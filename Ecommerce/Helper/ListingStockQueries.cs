using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Helper;

public static class ListingStockQueries
{
    public static async Task<MarketplaceStockRegister> ReadRegisterAsync(EcommerceDbContext db,int? vendorId,string search,int page,bool canEdit,CancellationToken ct)
    {
            var balances = db.Database.SqlQuery<MarketplaceStockRow>($"""
                SELECT l.VendorListingId AS ListingId,l.VendorId,v.Name AS VendorName,
                    p.Name AS ProductName,p.NameArb AS ProductNameArb,l.SellerSku,l.Status AS ListingStatus,
                    COALESCE(b.OnHandQuantity,CONVERT(bigint,0)) AS OnHand,
                    COALESCE(b.ReservedQuantity,CONVERT(bigint,0)) AS Reserved,
                    COALESCE(b.AvailableQuantity,CONVERT(bigint,0)) AS Available
                FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId
                JOIN dbo.Products p ON p.ProductId=l.ProductId
                LEFT JOIN dbo.InventoryBalances b ON b.VendorListingId=l.VendorListingId
                """);
            if (vendorId.HasValue) balances = balances.Where(x => x.VendorId == vendorId);
            if (search.Length > 0) balances = balances.Where(x => x.ProductName.Contains(search) ||
                (x.ProductNameArb != null && x.ProductNameArb.Contains(search)) || x.SellerSku.Contains(search) || x.VendorName.Contains(search));
            var rows = await balances.OrderBy(x => x.VendorId).ThenBy(x => x.ListingId)
                .Skip((page - 1) * 25).Take(26).ToListAsync(ct);
            return new MarketplaceStockRegister { VendorId = vendorId, Search = search, Page = page,
                CanEdit = canEdit, HasNext = rows.Count > 25, Rows = rows.Take(25).ToList() };
    }

    public static async Task<ListingStockPage?> ReadAsync(EcommerceDbContext db,int id,int listingId,int page,bool canEdit,CancellationToken ct)
    {
        var ar = System.Globalization.CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
        var product = await db.VendorListings.AsNoTracking().Where(l => l.VendorId == id &&
            l.VendorListingId == listingId).Select(l => ar && l.Product.NameArb != null && l.Product.NameArb != "" ? l.Product.NameArb : l.Product.Name)
            .SingleOrDefaultAsync(ct);
        if (product == null) return null;
        var result = await db.Database.SqlQuery<ListingStockPage>($"""
            SELECT {id} AS VendorId,{listingId} AS ListingId,{product} AS ProductName,
                COALESCE(b.OnHandQuantity,CONVERT(bigint,0)) AS OnHand,
                COALESCE(b.ReservedQuantity,CONVERT(bigint,0)) AS Reserved,
                COALESCE(b.AvailableQuantity,CONVERT(bigint,0)) AS Available,
                CONVERT(bit,CASE WHEN b.VendorListingId IS NULL THEN 0 ELSE 1 END) AS HasBalance,
                CONVERT(bit,0) AS CanEdit,1 AS Page,CONVERT(bit,0) AS HasNext
            FROM dbo.VendorListings l LEFT JOIN dbo.InventoryBalances b ON b.VendorListingId=l.VendorListingId
            WHERE l.VendorId={id} AND l.VendorListingId={listingId}
            """).SingleOrDefaultAsync(ct);
        if (result == null) return null;
        page = Math.Clamp(page, 1, 100000);
        var rows = await db.Database.SqlQuery<StockMovementRow>($"""
            SELECT t.InventoryTransactionId,t.OnHandDelta,t.ReservedDelta,t.SourceType,t.Reason,t.CreatedAtUtc
            FROM dbo.InventoryTransactions t JOIN dbo.VendorListings l ON l.VendorListingId=t.VendorListingId
            WHERE l.VendorId={id} AND l.VendorListingId={listingId}
            """).OrderByDescending(x => x.InventoryTransactionId).Skip((page - 1)*25).Take(26).ToListAsync(ct);
        result.Page = page;
        result.HasNext = rows.Count > 25;
        result.Movements = rows.Take(25).ToList();
        result.CanEdit = canEdit;
        result.Adjustment = new StockAdjustmentForm { RequestId=Guid.NewGuid(), Direction="Add", Quantity=1 };
        return result;
    }
}
