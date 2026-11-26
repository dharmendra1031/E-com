namespace EcommerceWebApplication.Models.ViewModels;

public class SellerCartLine
{
    public int CartItemId { get; set; }
    public int ListingId { get; set; }
    public int VendorId { get; set; }
    public string VendorName { get; set; } = "";
    public string? VendorNameArb { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductNameArb { get; set; }
    public int Quantity { get; set; }
    public decimal SavedPrice { get; set; }
    public decimal? CurrentPrice { get; set; }
    public long AvailableQuantity { get; set; }
}
