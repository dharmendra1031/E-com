using EcommerceWebApplication.Data;
using MF.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EcommerceWebApplication.Helper;

public static class MarketplacePaymentReconciliation
{
    public static bool SandboxEnabled(IConfiguration configuration, IWebHostEnvironment environment) =>
        SellerCartAccess.CheckoutEnabled(configuration, environment) &&
        configuration.GetValue("Marketplace:SellerSandboxPaymentEnabled", false) &&
        string.Equals(configuration["ApiUrl"]?.TrimEnd('/'), "https://apitest.myfatoorah.com", StringComparison.OrdinalIgnoreCase);

    // Caller must authorize the order first. Only persisted invoice IDs can be verified here.
    public static async Task<bool> VerifyLinkedAsync(EcommerceDbContext db, IPaymentService gateway,
        int orderId, Guid reference, decimal amount, CancellationToken ct)
    {
        var invoices = await db.Database.SqlQuery<long>($"""
            SELECT InvoiceId AS Value FROM dbo.MarketplacePaymentAttempts
            WHERE OrderId={orderId} AND MerchantReference={reference} AND InvoiceId IS NOT NULL
                AND State IN ('Linked','Paid')
            """).ToListAsync(ct);
        if (invoices.Count != 1) return false;
        var invoice = invoices[0];
        ct.ThrowIfCancellationRequested();
        var response = await gateway.GetPaymentStatus(new GetPaymentStatusRequest
        {
            KeyType = "InvoiceId", Key = invoice.ToString(CultureInfo.InvariantCulture)
        });
        ct.ThrowIfCancellationRequested();
        var verifiedId = MarketplacePaymentEvidence.VerifiedPaymentId(response, invoice, reference, amount);
        if (verifiedId == null) return false;
        await ConfirmAsync(db, orderId, reference, invoice, verifiedId, amount, ct);
        return true;
    }

    internal static async Task ConfirmAsync(EcommerceDbContext db, int orderId, Guid reference,
        long invoice, string paymentId, decimal amount, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.ConfirmMarketplacePayment @OrderId={orderId}, @InvoiceId={invoice}, @MerchantReference={reference}, @PaymentId={paymentId}, @Amount={amount}, @Currency={"KWD"}", ct);
}
