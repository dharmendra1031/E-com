using EcommerceWebApplication.Data;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Helper;

// Opt-in sandbox only. Never releases an order for which payment has started.
public sealed class MarketplaceReservationExpiryWorker(IServiceScopeFactory scopes,
    ILogger<MarketplaceReservationExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        int lastOrderId = 0;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
                var ids = await db.Database.SqlQuery<int>($"""
                    SELECT TOP (50) o.OrderId AS Value
                    FROM dbo.Orders o
                    WHERE o.OrderId>{lastOrderId} AND o.MarketplaceRequestId IS NOT NULL AND o.PaymentStatus=2
                      AND NOT EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts p WHERE p.OrderId=o.OrderId)
                      AND NOT EXISTS(SELECT 1 FROM dbo.Payments p WHERE p.OrderId=o.OrderId)
                      AND EXISTS(SELECT 1 FROM dbo.InventoryReservations r WHERE r.OrderId=o.OrderId)
                      AND NOT EXISTS(SELECT 1 FROM dbo.InventoryReservations r WHERE r.OrderId=o.OrderId
                          AND (r.Status<>'Reserved' OR r.ExpiresAtUtc>SYSUTCDATETIME()))
                    ORDER BY o.OrderId
                    """).ToListAsync(stoppingToken);
                foreach (var orderId in ids)
                {
                    lastOrderId = orderId;
                    try
                    {
                        await db.Database.ExecuteSqlInterpolatedAsync(
                            $"EXEC dbo.ReleaseExpiredUnstartedMarketplaceOrder @OrderId={orderId}", stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogWarning(ex, "Expired marketplace reservation requires review for order {OrderId}.", orderId);
                    }
                }
                if (ids.Count < 50) lastOrderId = 0;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Marketplace reservation expiry scan failed.");
            }
        }
    }
}
