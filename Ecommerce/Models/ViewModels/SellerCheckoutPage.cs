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
    public int OrderId { get; set; }
    public decimal Total { get; set; }
    public int PaymentStatus { get; set; }
}
