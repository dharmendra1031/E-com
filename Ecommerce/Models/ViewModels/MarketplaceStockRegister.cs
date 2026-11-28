namespace EcommerceWebApplication.Models.ViewModels;

public sealed class MarketplaceStockRegister
{
    public int? VendorId { get; set; }
    public string Search { get; set; } = "";
    public int Page { get; set; }
    public bool HasNext { get; set; }
    public bool CanEdit { get; set; }
    public List<MarketplaceStockRow> Rows { get; set; } = new();
}
public sealed class MarketplaceStockRow
{
    public int ListingId { get; set; }
    public int VendorId { get; set; }
    public string VendorName { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? ProductNameArb { get; set; }
    public string SellerSku { get; set; } = "";
    public string ListingStatus { get; set; } = "";
    public long OnHand { get; set; }
    public long Reserved { get; set; }
    public long Available { get; set; }
}
