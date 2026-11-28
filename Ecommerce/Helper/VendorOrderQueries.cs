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
                AND EXISTS(SELECT 1 FROM dbo.VerifiedMarketplacePaidOrders p WHERE p.OrderId=o.OrderId)
            """);
        if(vendorId.HasValue) query=query.Where(o=>o.VendorId==vendorId.Value);
        if(ValidStatus(status)) query=query.Where(o=>o.FulfillmentStatus==status);
        return await query.OrderByDescending(o=>o.VendorOrderId).Skip((page-1)*25).Take(26).ToListAsync(ct);
    }

    public static Task<List<VendorOrderStatusCount>> ReadCountsAsync(EcommerceDbContext db,int vendorId,CancellationToken ct) =>
        db.Database.SqlQuery<VendorOrderStatusCount>($"""
            SELECT v.FulfillmentStatus,COUNT(*) AS Total
            FROM dbo.VendorOrders v JOIN dbo.VerifiedMarketplacePaidOrders p ON p.OrderId=v.OrderId
            WHERE v.VendorId={vendorId} GROUP BY v.FulfillmentStatus
            """).ToListAsync(ct);

    public static bool ValidReportRange(DateTime from,DateTime to,DateTime today) =>
        from.Date<=to.Date && to.Date<=today.Date && (to.Date-from.Date).TotalDays<90;

    public static Task<List<VendorSalesDay>> ReadSalesDaysAsync(EcommerceDbContext db,int vendorId,DateTime from,DateTime until,CancellationToken ct) =>
        db.Database.SqlQuery<VendorSalesDay>($"""
            SELECT CONVERT(date,a.UpdatedAtUtc) AS Day,COUNT_BIG(*) AS OrderCount,
                CONVERT(decimal(28,3),SUM(v.MerchandiseSubtotal)) AS Merchandise
            FROM dbo.VendorOrders v JOIN dbo.VerifiedMarketplacePaidOrders p ON p.OrderId=v.OrderId
            JOIN dbo.MarketplacePaymentAttempts a ON a.OrderId=v.OrderId
            WHERE v.VendorId={vendorId} AND a.UpdatedAtUtc>={from} AND a.UpdatedAtUtc<{until}
            GROUP BY CONVERT(date,a.UpdatedAtUtc)
            """).OrderBy(x=>x.Day).ToListAsync(ct);

    public static Task<List<VendorReturnRow>> ReadReturnsAsync(EcommerceDbContext db,int vendorId,int page,CancellationToken ct) =>
        db.Database.SqlQuery<VendorReturnRow>($"""
            SELECT q.ReturnId,v.VendorOrderId,i.ProductNameSnapshot AS ProductName,i.ProductNameArbSnapshot AS ProductNameArb,
                q.Quantity,q.Status,q.RequestedAtUtc
            FROM dbo.MarketplaceReturnRequests q JOIN dbo.OrderItems i ON i.OrderItemId=q.OrderItemId
            JOIN dbo.VendorOrders v ON v.VendorOrderId=i.VendorOrderId AND v.OrderId=i.OrderId
            JOIN dbo.VerifiedMarketplacePaidOrders p ON p.OrderId=v.OrderId
            WHERE v.VendorId={vendorId}
            """).OrderByDescending(x=>x.ReturnId).Skip((page-1)*25).Take(26).ToListAsync(ct);

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
                AND EXISTS(SELECT 1 FROM dbo.VerifiedMarketplacePaidOrders p WHERE p.OrderId=o.OrderId)
            """).ToListAsync(ct);
}
