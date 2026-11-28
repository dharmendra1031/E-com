using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Helper;

public static class VendorOrderQueries
{
    public static bool ValidStatus(string? status) => status is "AwaitingPacking" or "Packed" or "HandedOver" or "Delivered";

    public static bool CanAdvance(string? expected, string? next, bool admin = false, bool deliveryConfirmed = false) =>
        (expected == "AwaitingPacking" && next == "Packed") ||
        (expected == "Packed" && next == "HandedOver") ||
        (admin && deliveryConfirmed && expected == "HandedOver" && next == "Delivered");

    public static async Task<List<VendorOrderRow>> ReadPageAsync(EcommerceDbContext db,int? vendorId,
        string? status,int page,CancellationToken ct)
    {
        var query = db.Database.SqlQuery<VendorOrderRow>($"""
            SELECT v.VendorOrderId,v.VendorId,v.VendorName,v.OrderId,v.MerchandiseSubtotal,o.CreatedAt,v.FulfillmentStatus,
                (SELECT COUNT(*) FROM dbo.OrderItems i WHERE i.VendorOrderId=v.VendorOrderId) AS ItemCount
            FROM dbo.VendorOrders v JOIN dbo.Orders o ON o.OrderId=v.OrderId
             WHERE o.PaymentStatus=1 AND o.MarketplaceRequestId IS NOT NULL
                AND EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts p WHERE p.OrderId=o.OrderId AND p.State='Paid')
            """);
        if(vendorId.HasValue) query=query.Where(o=>o.VendorId==vendorId.Value);
        if(ValidStatus(status)) query=query.Where(o=>o.FulfillmentStatus==status);
        return await query.OrderByDescending(o=>o.VendorOrderId).Skip((page-1)*25).Take(26).ToListAsync(ct);
    }

    public static Task<List<VendorFulfillmentHistoryRow>> ReadHistoryAsync(EcommerceDbContext db,int vendorId,int vendorOrderId,CancellationToken ct) =>
        db.Database.SqlQuery<VendorFulfillmentHistoryRow>($"""
            SELECT h.HistoryId,h.PreviousStatus,h.NewStatus,h.ChangedBy,h.ChangedAtUtc
            FROM dbo.VendorOrderFulfillmentHistory h JOIN dbo.VendorOrders v ON v.VendorOrderId=h.VendorOrderId
            WHERE v.VendorId={vendorId} AND v.VendorOrderId={vendorOrderId}
            """).OrderBy(h=>h.HistoryId).ToListAsync(ct);

    public static Task<List<VendorOrderLine>> ReadLinesAsync(EcommerceDbContext db,int vendorId,int vendorOrderId,CancellationToken ct) =>
        db.Database.SqlQuery<VendorOrderLine>($"""
            SELECT i.ProductNameSnapshot AS ProductName,i.ProductNameArbSnapshot AS ProductNameArb,
                i.SellerSkuSnapshot AS SellerSku,i.Quantity,i.Price,v.FulfillmentStatus
            FROM dbo.OrderItems i JOIN dbo.VendorOrders v ON v.VendorOrderId=i.VendorOrderId AND v.OrderId=i.OrderId
            JOIN dbo.Orders o ON o.OrderId=v.OrderId
            WHERE v.VendorId={vendorId} AND v.VendorOrderId={vendorOrderId} AND o.PaymentStatus=1
                AND o.MarketplaceRequestId IS NOT NULL
                AND EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts p WHERE p.OrderId=o.OrderId AND p.State='Paid')
            """).ToListAsync(ct);
}
