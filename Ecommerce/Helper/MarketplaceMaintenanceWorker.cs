using EcommerceWebApplication.Data;
using MF.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Helper;

// One opt-in development worker for safe expiry and read-only gateway polling.
// Only authenticated, verified gateway evidence can trigger financial posting.
public sealed class MarketplaceMaintenanceWorker(IServiceScopeFactory scopes,
    IConfiguration configuration, IWebHostEnvironment environment,
    ILogger<MarketplaceMaintenanceWorker> logger) : BackgroundService
{
    private int lastExpiryId;
    private int lastPaymentId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!SellerCartAccess.CheckoutEnabled(configuration, environment)) continue;
            if (configuration.GetValue("Marketplace:ReservationExpiryWorkerEnabled", false))
            {
                try { await ReleaseExpiredAsync(stoppingToken); }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                { logger.LogWarning(ex, "Marketplace reservation expiry scan failed."); }
            }
            if (configuration.GetValue("Marketplace:PaymentReconciliationWorkerEnabled", false) &&
                MarketplacePaymentReconciliation.SandboxEnabled(configuration, environment))
            {
                try { await ReconcileAsync(stoppingToken); }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                { logger.LogWarning(ex, "Marketplace payment reconciliation scan failed."); }
            }
        }
    }

    private async Task ReleaseExpiredAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
        var ids = await db.Database.SqlQuery<int>($"""
            SELECT TOP (50) o.OrderId AS Value
            FROM dbo.Orders o
            WHERE o.OrderId>{lastExpiryId} AND o.MarketplaceRequestId IS NOT NULL AND o.PaymentStatus=2
              AND NOT EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts p WHERE p.OrderId=o.OrderId)
              AND NOT EXISTS(SELECT 1 FROM dbo.Payments p WHERE p.OrderId=o.OrderId)
              AND EXISTS(SELECT 1 FROM dbo.InventoryReservations r WHERE r.OrderId=o.OrderId)
              AND NOT EXISTS(SELECT 1 FROM dbo.InventoryReservations r WHERE r.OrderId=o.OrderId
                  AND (r.Status<>'Reserved' OR r.ExpiresAtUtc>SYSUTCDATETIME()))
            ORDER BY o.OrderId
            """).ToListAsync(ct);
        foreach (var orderId in ids)
        {
            ct.ThrowIfCancellationRequested();
            if (!SellerCartAccess.CheckoutEnabled(configuration, environment) ||
                !configuration.GetValue("Marketplace:ReservationExpiryWorkerEnabled", false)) return;
            lastExpiryId = orderId;
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC dbo.ReleaseExpiredUnstartedMarketplaceOrder @OrderId={orderId}", ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Expired marketplace reservation requires review for order {OrderId}.", orderId);
            }
        }
        if (ids.Count < 50) lastExpiryId = 0;
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var rows = await db.Database.SqlQuery<PaymentCandidate>($"""
            SELECT TOP (10) OrderId,MerchantReference,ExpectedAmount
            FROM dbo.MarketplaceLinkedPaymentCandidates
            WHERE OrderId>{lastPaymentId} AND UpdatedAtUtc<=DATEADD(minute,-1,SYSUTCDATETIME())
            ORDER BY OrderId
            """).ToListAsync(ct);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if (!MarketplacePaymentReconciliation.SandboxEnabled(configuration, environment) ||
                !configuration.GetValue("Marketplace:PaymentReconciliationWorkerEnabled", false)) return;
            lastPaymentId = row.OrderId;
            try
            {
                await MarketplacePaymentReconciliation.VerifyLinkedAsync(db, gateway,
                    row.OrderId, row.MerchantReference, row.ExpectedAmount, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Marketplace payment remains under review for order {OrderId}. Error type: {ErrorType}.", row.OrderId, ex.GetType().Name);
            }
        }
        if (rows.Count < 10) lastPaymentId = 0;
    }

    public sealed class PaymentCandidate
    {
        public int OrderId { get; set; }
        public Guid MerchantReference { get; set; }
        public decimal ExpectedAmount { get; set; }
    }
}
