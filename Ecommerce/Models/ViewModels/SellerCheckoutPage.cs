using EcommerceWebApplication.Helper;

namespace EcommerceWebApplication.Models.ViewModels;

public class SellerCheckoutPage
{
    public List<SellerCartLine> Lines { get; set; } = new();
    public List<UserAddress> Addresses { get; set; } = new();
    public SellerCheckoutQuote Quote { get; set; } = null!;
    public string Token { get; set; } = "";
}

public class SellerOrderReceipt
{
    public Guid RequestId { get; set; }
    public int OrderId { get; set; }
    public decimal Total { get; set; }
    public int PaymentStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public string AttemptState { get; set; } = "";
    public string ReservationState { get; set; } = "";
}

public class CustomerSuborderRow
{
    public int VendorOrderId { get; set; }
    public string VendorName { get; set; } = "";
    public string? VendorNameArb { get; set; }
    public decimal MerchandiseSubtotal { get; set; }
    public string FulfillmentStatus { get; set; } = "";
}
