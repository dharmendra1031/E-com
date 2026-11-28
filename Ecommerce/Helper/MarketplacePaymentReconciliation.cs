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

    // The signature is a notification, not financial evidence. Recover the stored order
    // only from the authenticated gateway's exact merchant reference.
    public static async Task<bool> VerifyWebhookAsync(EcommerceDbContext db, IPaymentService gateway,
        MarketplaceWebhookSignature.PaymentEvent payment, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var response = await gateway.GetPaymentStatus(new GetPaymentStatusRequest
        {
            KeyType = "PaymentId", Key = payment.PaymentId
        });
        ct.ThrowIfCancellationRequested();
        if (response?.IsSuccess != true || response.Data == null ||
            response.Data.InvoiceId != payment.InvoiceId ||
            !Guid.TryParseExact(response.Data.CustomerReference, "N", out var reference)) return false;
        var candidates = await db.Database.SqlQuery<WebhookCandidate>($"""
            SELECT p.OrderId,p.ExpectedAmount,p.State,p.InvoiceId
            FROM dbo.MarketplacePaymentAttempts p
            JOIN dbo.Orders o ON o.OrderId=p.OrderId AND o.MarketplaceRequestId=p.MerchantReference
            WHERE p.MerchantReference={reference} AND p.ExpectedAmount=o.TotalAmount AND p.Currency='KWD'
              AND ((p.State='Starting' AND p.InvoiceId IS NULL AND o.PaymentStatus=2)
                OR (p.State='Linked' AND o.PaymentStatus=2)
                OR (p.State='Paid' AND o.PaymentStatus=1))
            """).ToListAsync(ct);
        if (candidates.Count != 1) return false;
        var candidate = candidates[0];
        if (candidate.InvoiceId.HasValue && candidate.InvoiceId.Value != payment.InvoiceId) return false;
        var verified = MarketplacePaymentEvidence.VerifiedRecoveryInvoice(response, payment.PaymentId,
            reference, candidate.ExpectedAmount);
        if (verified != payment.InvoiceId) return false;
        if (candidate.State == "Starting")
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.LinkMarketplaceInvoice @OrderId={candidate.OrderId}, @InvoiceId={payment.InvoiceId}, @MerchantReference={reference}", ct);
        await ConfirmAsync(db, candidate.OrderId, reference, payment.InvoiceId, payment.PaymentId, candidate.ExpectedAmount, ct);
        return true;
    }

    public sealed class WebhookCandidate
    {
        public int OrderId { get; set; }
        public decimal ExpectedAmount { get; set; }
        public string State { get; set; } = "";
        public long? InvoiceId { get; set; }
    }

    internal static async Task ConfirmAsync(EcommerceDbContext db, int orderId, Guid reference,
        long invoice, string paymentId, decimal amount, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.ConfirmMarketplacePayment @OrderId={orderId}, @InvoiceId={invoice}, @MerchantReference={reference}, @PaymentId={paymentId}, @Amount={amount}, @Currency={"KWD"}", ct);
}
