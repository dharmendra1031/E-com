namespace EcommerceWebApplication.Models.ViewModels;
public class MarketplaceReturnItem
{
    public int OrderItemId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductNameArb { get; set; }
    public int PurchasedQuantity { get; set; }
    public int RemainingQuantity { get; set; }
}
public class MarketplaceReturnRow
{
    public int ReturnId { get; set; }
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductNameArb { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime RequestedAtUtc { get; set; }
}
