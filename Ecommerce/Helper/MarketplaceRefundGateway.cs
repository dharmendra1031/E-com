using System.Globalization;
using MF.Models;
using Newtonsoft.Json;

namespace EcommerceWebApplication.Helper;

// V2 refund endpoints. A creation response is never proof that funds were returned.
public static class MarketplaceRefundGateway
{
    public static bool Enabled(IConfiguration configuration, IWebHostEnvironment environment) =>
        MarketplacePaymentReconciliation.SandboxEnabled(configuration, environment) &&
        configuration.GetValue("Marketplace:FinancePreviewEnabled", false) &&
        configuration.GetValue("Marketplace:SandboxRefundEnabled", false);

    public sealed class RefundData
    {
        public List<RefundEntry>? RefundStatusResult { get; set; }
    }
    public sealed class RefundEntry
    {
        public long RefundId { get; set; }
        public long InvoiceId { get; set; }
        public string? RefundStatus { get; set; }
        public string? ExternalIdentifier { get; set; }
        public string? BaseCurrency { get; set; }
        public decimal? Amount { get; set; }
        public decimal? RefundAmount { get; set; }
    }

    public static long? VerifiedRefundId(GenericResponse<RefundData>? response,long invoice,Guid reference,decimal amount)
    {
        if(response?.IsSuccess!=true || response.Data?.RefundStatusResult==null || invoice<=0 || reference==Guid.Empty || amount<=0)
            return null;
        var matches=response.Data.RefundStatusResult.Where(r=>r!=null && r.ExternalIdentifier==reference.ToString("N")).ToList();
        if(matches.Count!=1) return null;
        var r=matches[0];
        return r.RefundId>0 && r.InvoiceId==invoice && r.RefundStatus=="Refunded" &&
            r.BaseCurrency=="KWD" && r.Amount==amount && r.RefundAmount==amount ? r.RefundId : null;
    }

    public static async Task<GenericResponse<RefundData>?> InquiryAsync(IMFClient gateway,long invoice)
    {
        var json=JsonConvert.SerializeObject(new { KeyType="InvoiceId",Key=invoice.ToString(CultureInfo.InvariantCulture) });
        return JsonConvert.DeserializeObject<GenericResponse<RefundData>>(
            await gateway.PerformRequest(json,endPoint:"GetRefundStatus"));
    }

    public static async Task RequestAsync(IMFClient gateway,long invoice,Guid reference,decimal amount)
    {
        var json=JsonConvert.SerializeObject(new { KeyType="InvoiceId",Key=invoice.ToString(CultureInfo.InvariantCulture),
            Amount=amount,ExternalIdentifier=reference.ToString("N"),ServiceChargeOnCustomer=false,
            Comment="Marketplace sandbox return" });
        // Never retry this mutation, even after a timeout. Recover through inquiry.
        await gateway.PerformRequest(json,endPoint:"MakeRefund",idempotencyKey:reference.ToString("N"));
    }
}
