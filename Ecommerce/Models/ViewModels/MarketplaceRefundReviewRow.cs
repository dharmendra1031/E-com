namespace EcommerceWebApplication.Models.ViewModels;
public class MarketplaceRefundReviewRow
{
    public int ReturnId { get; set; }
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public int VendorId { get; set; }
    public string VendorName { get; set; }="";
    public string ProductName { get; set; }="";
    public int Quantity { get; set; }
    public decimal OriginalUnitPrice { get; set; }
    public decimal ReturnedMerchandiseValue { get; set; }
    public long InvoiceId { get; set; }
    public string PaymentId { get; set; }="";
    public DateTime ReceivedAtUtc { get; set; }
    public long ReturnReceiptId { get; set; }
    public Guid? RefundReference { get; set; }
    public decimal? RefundAmount { get; set; }
    public long? GatewayRefundId { get; set; }
}
