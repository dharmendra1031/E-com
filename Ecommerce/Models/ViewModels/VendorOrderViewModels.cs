namespace EcommerceWebApplication.Models.ViewModels;
public class VendorOrderRow
{
    public int VendorOrderId { get; set; }
    public int VendorId { get; set; }
    public string VendorName { get; set; } = "";
    public int OrderId { get; set; }
    public decimal MerchandiseSubtotal { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
    public string FulfillmentStatus { get; set; } = "";
}
public class VendorOrderLine
{
    public string FulfillmentStatus { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? ProductNameArb { get; set; }
    public string SellerSku { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public sealed class VendorFulfillmentHistoryRow
{
    public long HistoryId { get; set; }
    public string PreviousStatus { get; set; } = "";
    public string NewStatus { get; set; } = "";
    public int ChangedBy { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
