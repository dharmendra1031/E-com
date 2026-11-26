namespace EcommerceWebApplication.Models.ViewModels;

public class MarketplaceOffer
{
    public int ListingId { get; set; }
    public int ProductId { get; set; }
    public string VendorName { get; set; } = "";
    public string? VendorNameArb { get; set; }
    public decimal Price { get; set; }
    public long AvailableQuantity { get; set; }
}
