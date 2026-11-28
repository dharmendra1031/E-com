SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- Read model only. Every application query MUST constrain CustomerId.
CREATE OR ALTER VIEW dbo.MarketplaceCustomerOrders
AS
SELECT o.OrderId,o.CustomerId,o.MarketplaceRequestId AS RequestId,o.TotalAmount AS Total,
       o.PaymentStatus,o.CreatedAt,CASE WHEN (o.PaymentStatus=1 OR p.State='Paid')
            AND NOT EXISTS(SELECT 1 FROM dbo.VerifiedMarketplacePaidOrders paid WHERE paid.OrderId=o.OrderId)
            THEN 'Review' ELSE COALESCE(p.State,'NotStarted') END AS AttemptState,
       CASE WHEN r.LineCount=0 THEN 'Missing'
            WHEN r.ReleasedCount=r.LineCount THEN 'Released'
            WHEN r.ConsumedCount=r.LineCount THEN 'Consumed'
            WHEN r.ReservedCount=r.LineCount AND r.MinExpiry>SYSUTCDATETIME() THEN 'Reserved'
            WHEN r.ReservedCount=r.LineCount THEN 'Expired'
            ELSE 'Review' END AS ReservationState
FROM dbo.Orders o LEFT JOIN dbo.MarketplacePaymentAttempts p ON p.OrderId=o.OrderId
CROSS APPLY(SELECT COUNT(*) AS LineCount,
    COUNT(CASE WHEN Status='Released' THEN 1 END) AS ReleasedCount,
    COUNT(CASE WHEN Status='Consumed' THEN 1 END) AS ConsumedCount,
    COUNT(CASE WHEN Status='Reserved' THEN 1 END) AS ReservedCount,
    MIN(ExpiresAtUtc) AS MinExpiry FROM dbo.InventoryReservations r WHERE r.OrderId=o.OrderId) r
WHERE o.MarketplaceRequestId IS NOT NULL;
GO
