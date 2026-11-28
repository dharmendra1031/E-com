SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO
-- Sandbox merchandise ledger only; not an approved payable or payout.
IF OBJECT_ID(N'dbo.VendorSettlementLedger',N'U') IS NULL
CREATE TABLE dbo.VendorSettlementLedger(
    EntryId bigint IDENTITY PRIMARY KEY,
    VendorOrderId int NOT NULL REFERENCES dbo.VendorOrders(VendorOrderId),
    ReturnId int NULL REFERENCES dbo.MarketplaceRefundAttempts(ReturnId),
    MerchandiseDelta decimal(18,3) NOT NULL,
    CommissionDelta decimal(18,3) NOT NULL,
    PostedBy int NOT NULL REFERENCES dbo.Users(UserId),
    PostedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_VendorSettlementLedger_Direction CHECK(
        (ReturnId IS NULL AND MerchandiseDelta>=0 AND CommissionDelta BETWEEN 0 AND MerchandiseDelta)
        OR (ReturnId IS NOT NULL AND MerchandiseDelta<0 AND CommissionDelta BETWEEN MerchandiseDelta AND 0))
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.VendorSettlementLedger') AND name='UX_VendorSettlement_Sale')
    CREATE UNIQUE INDEX UX_VendorSettlement_Sale ON dbo.VendorSettlementLedger(VendorOrderId) WHERE ReturnId IS NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.VendorSettlementLedger') AND name='UX_VendorSettlement_Return')
    CREATE UNIQUE INDEX UX_VendorSettlement_Return ON dbo.VendorSettlementLedger(ReturnId) WHERE ReturnId IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.VendorSettlementLedger') AND name='IX_VendorSettlement_Suborder')
    CREATE INDEX IX_VendorSettlement_Suborder ON dbo.VendorSettlementLedger(VendorOrderId) INCLUDE(ReturnId,MerchandiseDelta,CommissionDelta);
GO
CREATE OR ALTER VIEW dbo.VerifiedSettlementRefunds
AS
SELECT i.VendorOrderId,a.ReturnId,a.Amount,r.Quantity
FROM dbo.MarketplaceRefundAttempts a
JOIN dbo.MarketplaceRefundReview r ON r.ReturnId=a.ReturnId AND r.OrderId=a.OrderId AND r.InvoiceId=a.InvoiceId
JOIN dbo.OrderItems i ON i.OrderItemId=r.OrderItemId AND i.OrderId=a.OrderId
WHERE a.GatewayRefundId IS NOT NULL AND a.Amount<=r.ReturnedMerchandiseValue;
GO
CREATE OR ALTER VIEW dbo.MarketplaceSettlementReview
AS
SELECT f.*,
    CONVERT(decimal(18,3),COALESCE(refunds.Amount,0)) AS VerifiedRefundValue,
    CONVERT(decimal(18,3),ledger.Net) AS LedgerNet,
    CONVERT(decimal(18,3),ledger.ReversedCommission) AS LedgerCommissionReversal,
    CONVERT(bit,CASE WHEN f.CommissionAmount IS NOT NULL AND f.FulfillmentStatus='Delivered'
        AND f.OpenReturnQuantity=0 AND f.ReceivedReturnQuantity=COALESCE(refunds.Qty,0)
        AND COALESCE(refunds.Amount,0)<=f.MerchandiseSubtotal
        AND NOT EXISTS(SELECT 1 FROM dbo.OrderItems i JOIN dbo.MarketplaceReturnRequests q ON q.OrderItemId=i.OrderItemId
            WHERE i.VendorOrderId=f.VendorOrderId AND i.OrderId=f.OrderId AND q.Status='Received'
                AND NOT EXISTS(SELECT 1 FROM dbo.VerifiedSettlementRefunds vr WHERE vr.ReturnId=q.ReturnId AND vr.VendorOrderId=f.VendorOrderId))
        AND EXISTS(SELECT 1 FROM dbo.Orders o JOIN dbo.MarketplacePaymentAttempts p ON p.OrderId=o.OrderId
            WHERE o.OrderId=f.OrderId AND p.State='Paid' AND p.InvoiceId IS NOT NULL AND p.PaymentId IS NOT NULL
                AND p.ExpectedAmount=o.TotalAmount AND p.MerchantReference=o.MarketplaceRequestId AND p.Currency='KWD')
        AND EXISTS(SELECT 1 FROM dbo.VendorOrderFulfillmentHistory h WHERE h.VendorOrderId=f.VendorOrderId AND h.NewStatus='Delivered')
        THEN 1 ELSE 0 END) AS SettlementEligible,
    CONVERT(bit,CASE WHEN ledger.SaleCount=1 AND COALESCE(ledger.RefundTotal,0)=COALESCE(refunds.Amount,0)
        AND NOT EXISTS(SELECT 1 FROM dbo.VerifiedSettlementRefunds vr WHERE vr.VendorOrderId=f.VendorOrderId
            AND NOT EXISTS(SELECT 1 FROM dbo.VendorSettlementLedger e WHERE e.ReturnId=vr.ReturnId AND e.VendorOrderId=vr.VendorOrderId))
        THEN 1 ELSE 0 END) AS LedgerCurrent
FROM dbo.MarketplaceFinanceReview f
OUTER APPLY(SELECT SUM(vr.Amount) AS Amount,SUM(CONVERT(bigint,vr.Quantity)) AS Qty
    FROM dbo.VerifiedSettlementRefunds vr WHERE vr.VendorOrderId=f.VendorOrderId) refunds
OUTER APPLY(SELECT SUM(MerchandiseDelta-CommissionDelta) AS Net,
    SUM(CASE WHEN ReturnId IS NOT NULL THEN -CommissionDelta ELSE 0 END) AS ReversedCommission,
    SUM(CASE WHEN ReturnId IS NOT NULL THEN -MerchandiseDelta ELSE 0 END) AS RefundTotal,
    SUM(CASE WHEN ReturnId IS NULL THEN 1 ELSE 0 END) AS SaleCount
    FROM dbo.VendorSettlementLedger e WHERE e.VendorOrderId=f.VendorOrderId) ledger;
GO
CREATE OR ALTER PROCEDURE dbo.PostVendorSettlementPreview
    @VendorOrderId int,@ActorId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Users u WITH(HOLDLOCK) JOIN dbo.[Role] r WITH(HOLDLOCK) ON r.RoleId=u.RoleId
            WHERE u.UserId=@ActorId AND u.IsActive=1 AND r.IsActive=1 AND r.RoleName='Admin')
            THROW 52701,'Admin required.',1;
        DECLARE @order int=(SELECT OrderId FROM dbo.VendorOrders WHERE VendorOrderId=@VendorOrderId);
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK)
            WHERE OrderId=@order AND PaymentStatus=1 AND MarketplaceRequestId IS NOT NULL)
            THROW 52702,'Paid marketplace order required.',1;
        -- Parent lock serializes returns and fulfillment. Refund confirmation has its own row lock.
        DECLARE @refundCount bigint;
        SELECT @refundCount=COUNT_BIG(*) FROM dbo.MarketplaceRefundAttempts WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@order;
        DECLARE @gross decimal(18,3),@commission decimal(18,3),@rate decimal(7,4);
        SELECT @gross=MerchandiseSubtotal,@commission=CommissionAmount,@rate=CommissionRatePercent
        FROM dbo.MarketplaceSettlementReview WHERE VendorOrderId=@VendorOrderId AND SettlementEligible=1;
        IF @gross IS NULL OR @commission IS NULL OR @rate IS NULL
            THROW 52703,'Delivery, policy or return evidence incomplete.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.VendorSettlementLedger WITH(UPDLOCK,HOLDLOCK)
            WHERE VendorOrderId=@VendorOrderId AND ReturnId IS NULL)
            INSERT dbo.VendorSettlementLedger(VendorOrderId,MerchandiseDelta,CommissionDelta,PostedBy)
            VALUES(@VendorOrderId,@gross,@commission,@ActorId);
        DECLARE @posted decimal(18,3),@reversed decimal(18,3);
        SELECT @posted=COALESCE(SUM(-MerchandiseDelta),0),@reversed=COALESCE(SUM(-CommissionDelta),0)
        FROM dbo.VendorSettlementLedger WITH(UPDLOCK,HOLDLOCK) WHERE VendorOrderId=@VendorOrderId AND ReturnId IS NOT NULL;
        IF @posted>@gross OR @reversed<>ROUND(@posted*@rate/100,3)
            OR NOT EXISTS(SELECT 1 FROM dbo.VendorSettlementLedger WHERE VendorOrderId=@VendorOrderId AND ReturnId IS NULL
                AND MerchandiseDelta=@gross AND CommissionDelta=@commission)
            OR EXISTS(SELECT 1 FROM dbo.VendorSettlementLedger e WHERE e.VendorOrderId=@VendorOrderId AND e.ReturnId IS NOT NULL
                AND NOT EXISTS(SELECT 1 FROM dbo.VerifiedSettlementRefunds vr WHERE vr.ReturnId=e.ReturnId
                    AND vr.VendorOrderId=e.VendorOrderId AND vr.Amount=-e.MerchandiseDelta))
            THROW 52704,'Existing ledger requires investigation.',1;
        -- Cumulative rounding preserves original commission exactly on a full refund.
        ;WITH missing AS (
            SELECT vr.ReturnId,vr.Amount,
                CONVERT(decimal(18,3),@posted+CONVERT(decimal(18,3),SUM(vr.Amount) OVER(ORDER BY vr.ReturnId ROWS UNBOUNDED PRECEDING))) AS Cumulative
            FROM dbo.VerifiedSettlementRefunds vr
            WHERE vr.VendorOrderId=@VendorOrderId AND NOT EXISTS(
                SELECT 1 FROM dbo.VendorSettlementLedger e WHERE e.ReturnId=vr.ReturnId)
        )
        INSERT dbo.VendorSettlementLedger(VendorOrderId,ReturnId,MerchandiseDelta,CommissionDelta,PostedBy)
        SELECT @VendorOrderId,ReturnId,-Amount,
            ROUND(CONVERT(decimal(18,3),Cumulative-Amount)*@rate/100,3)-ROUND(Cumulative*@rate/100,3),@ActorId
        FROM missing;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
