using System.ComponentModel.DataAnnotations;
namespace EcommerceWebApplication.Models.ViewModels;
public class VendorCommissionForm
{
    public int VendorId { get; set; }
    public int ExpectedPolicyId { get; set; }
    public string VendorName { get; set; } = "";
    [Required,Range(typeof(decimal),"0","100")]
    public decimal? RatePercent { get; set; }
}
public class MarketplaceFinanceRow
{
    public int VendorOrderId { get; set; }
    public int OrderId { get; set; }
    public int VendorId { get; set; }
    public string VendorName { get; set; } = "";
    public decimal MerchandiseSubtotal { get; set; }
    public decimal? CommissionRatePercent { get; set; }
    public decimal? CommissionAmount { get; set; }
    public decimal? MerchandiseAfterCommission { get; set; }
    public string FulfillmentStatus { get; set; } = "";
    public string ReviewState { get; set; } = "";
    public long ReceivedReturnQuantity { get; set; }
    public decimal ReturnedMerchandiseValue { get; set; }
    public decimal RemainingMerchandiseValue { get; set; }
    public long OpenReturnQuantity { get; set; }
    public decimal VerifiedRefundValue { get; set; }
    public decimal? LedgerNet { get; set; }
    public decimal? LedgerCommissionReversal { get; set; }
    public bool SettlementEligible { get; set; }
    public bool LedgerCurrent { get; set; }
}
