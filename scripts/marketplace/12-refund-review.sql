SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- Review evidence only, not authorization to issue a refund.
CREATE OR ALTER VIEW dbo.MarketplaceRefundReview
AS
SELECT q.ReturnId,q.OrderItemId,i.OrderId,vo.VendorId,vo.VendorName,
       i.ProductNameSnapshot AS ProductName,q.Quantity,i.Price AS OriginalUnitPrice,
       CONVERT(decimal(18,3),q.Quantity*i.Price) AS ReturnedMerchandiseValue,
       p.InvoiceId,p.PaymentId,q.ReceivedAtUtc,
       rr.ReturnReceiptId
FROM dbo.MarketplaceReturnRequests q
JOIN dbo.OrderItems i ON i.OrderItemId=q.OrderItemId
JOIN dbo.VendorOrders vo ON vo.VendorOrderId=i.VendorOrderId AND vo.OrderId=i.OrderId
JOIN dbo.Orders o ON o.OrderId=i.OrderId
JOIN dbo.VerifiedMarketplacePaidOrders p ON p.OrderId=o.OrderId
JOIN dbo.VendorReturnReceipts rr ON rr.ReceiptRequestId=q.RequestId AND rr.OrderItemId=q.OrderItemId AND rr.Quantity=q.Quantity
WHERE q.Status='Received' AND q.ReceivedAtUtc IS NOT NULL AND o.PaymentStatus=1
  AND p.InvoiceId IS NOT NULL AND p.PaymentId IS NOT NULL
  AND EXISTS(SELECT 1 FROM dbo.InventoryTransactions t WHERE t.VendorListingId=i.VendorListingId
      AND t.SourceType='ReturnReceipt' AND t.SourceId=rr.ReturnReceiptId AND t.SourceLineId=i.OrderItemId
      AND t.OnHandDelta=q.Quantity AND t.ReservedDelta=0);
GO
