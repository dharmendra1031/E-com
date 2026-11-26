using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Helper;

public static class MarketplaceOffers
{
    public static bool Enabled(IConfiguration configuration) =>
        configuration.GetValue("Marketplace:CustomerOfferPreviewEnabled", false);

    public static IQueryable<MarketplaceOffer> Available(EcommerceDbContext db) =>
        db.Database.SqlQuery<MarketplaceOffer>($"""
            SELECT ListingId, ProductId, VendorName, VendorNameArb, Price, AvailableQuantity
            FROM dbo.MarketplaceAvailableOffers
            """);
    public static IQueryable<MarketplaceOfferSummary> Summaries(EcommerceDbContext db) =>
        Available(db).GroupBy(o => o.ProductId).Select(g => new MarketplaceOfferSummary
        {
            ProductId = g.Key, LowestPrice = g.Min(o => o.Price), SellerCount = g.Count()
        });

    public static Dictionary<int, MarketplaceOfferSummary> ForProducts(EcommerceDbContext db,
        IEnumerable<int> productIds, IConfiguration configuration)
    {
        if (!Enabled(configuration)) return new();
        var ids = productIds.Distinct().ToArray();
        if (ids.Length == 0) return new();
        return Summaries(db).Where(o => ids.Contains(o.ProductId)).ToDictionary(o => o.ProductId);
    }
}
