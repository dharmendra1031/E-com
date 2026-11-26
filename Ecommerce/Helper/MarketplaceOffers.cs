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
}
