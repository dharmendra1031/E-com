namespace EcommerceWebApplication.Models.ViewModels;
public class MarketplacePaymentReviewRow
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public int PaymentStatus { get; set; }
    public long? InvoiceId { get; set; }
    public string AttemptState { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
