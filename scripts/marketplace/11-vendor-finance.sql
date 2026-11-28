SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'dbo.VendorCommissionPolicies',N'U') IS NULL
CREATE TABLE dbo.VendorCommissionPolicies(
    PolicyId int IDENTITY PRIMARY KEY,
    VendorId int NOT NULL REFERENCES dbo.Vendors(VendorId),
    RatePercent decimal(7,4) NOT NULL CHECK(RatePercent BETWEEN 0 AND 100),
    CreatedBy int NOT NULL REFERENCES dbo.Users(UserId),
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_CommissionPolicy_Vendor UNIQUE(VendorId,PolicyId)
);
GO
IF COL_LENGTH(N'dbo.VendorOrders',N'CommissionPolicyId') IS NULL
    ALTER TABLE dbo.VendorOrders ADD CommissionPolicyId int NULL,CommissionRatePercent decimal(7,4) NULL,
        CommissionAmount decimal(18,3) NULL;
GO
IF OBJECT_ID(N'dbo.FK_VendorOrders_CommissionPolicy',N'F') IS NULL
    ALTER TABLE dbo.VendorOrders WITH CHECK ADD CONSTRAINT FK_VendorOrders_CommissionPolicy
        FOREIGN KEY(VendorId,CommissionPolicyId) REFERENCES dbo.VendorCommissionPolicies(VendorId,PolicyId);
IF OBJECT_ID(N'dbo.CK_VendorOrders_CommissionSnapshot',N'C') IS NULL
    ALTER TABLE dbo.VendorOrders WITH CHECK ADD CONSTRAINT CK_VendorOrders_CommissionSnapshot CHECK(
        (CommissionPolicyId IS NULL AND CommissionRatePercent IS NULL AND CommissionAmount IS NULL)
        OR (CommissionPolicyId IS NOT NULL AND CommissionRatePercent IS NOT NULL AND CommissionAmount IS NOT NULL
            AND CommissionRatePercent BETWEEN 0 AND 100 AND CommissionAmount BETWEEN 0 AND MerchandiseSubtotal
            AND CommissionAmount=ROUND(MerchandiseSubtotal*CommissionRatePercent/100,3)));
GO
-- App superadmin policy also required; no implicit default rate.
CREATE OR ALTER PROCEDURE dbo.SetVendorCommission
    @VendorId int,@ActorId int,@ExpectedPolicyId int,@RatePercent decimal(7,4)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @RatePercent IS NULL OR @RatePercent NOT BETWEEN 0 AND 100 OR @ExpectedPolicyId IS NULL OR @ExpectedPolicyId<0
        THROW 52400,'Invalid commission settings.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Users u WITH(HOLDLOCK) JOIN dbo.[Role] r WITH(HOLDLOCK) ON r.RoleId=u.RoleId
            WHERE u.UserId=@ActorId AND u.IsActive=1 AND r.IsActive=1 AND r.RoleName='Admin')
            THROW 52401,'Admin required.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.Vendors WITH(UPDLOCK,HOLDLOCK) WHERE VendorId=@VendorId)
            THROW 52402,'Vendor missing.',1;
        DECLARE @current int,@rate decimal(7,4);
        SELECT TOP(1) @current=PolicyId,@rate=RatePercent FROM dbo.VendorCommissionPolicies WITH(HOLDLOCK)
            WHERE VendorId=@VendorId ORDER BY PolicyId DESC;
        IF COALESCE(@current,0)<>@ExpectedPolicyId THROW 52403,'Policy changed. Reload.',1;
        IF @rate IS NULL OR @rate<>@RatePercent
            INSERT dbo.VendorCommissionPolicies(VendorId,RatePercent,CreatedBy) VALUES(@VendorId,@RatePercent,@ActorId);
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
-- Called only during original order creation in the same outer transaction.
CREATE OR ALTER PROCEDURE dbo.SnapshotVendorCommission
    @OrderId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @@TRANCOUNT=0 THROW 52404,'Outer order transaction required.',1;
    BEGIN TRY
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@OrderId AND MarketplaceRequestId IS NOT NULL AND PaymentStatus=2)
           OR EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WHERE OrderId=@OrderId)
           OR NOT EXISTS(SELECT 1 FROM dbo.VendorOrders WHERE OrderId=@OrderId)
            THROW 52404,'New unpaid order required.',1;
        IF EXISTS(SELECT 1 FROM dbo.VendorOrders WHERE OrderId=@OrderId AND CommissionPolicyId IS NOT NULL)
            THROW 52405,'Commission snapshot already exists.',1;
        UPDATE vo SET CommissionPolicyId=p.PolicyId,CommissionRatePercent=p.RatePercent,
            CommissionAmount=ROUND(vo.MerchandiseSubtotal*p.RatePercent/100,3)
        FROM dbo.VendorOrders vo
        CROSS APPLY(SELECT TOP(1) PolicyId,RatePercent FROM dbo.VendorCommissionPolicies WITH(HOLDLOCK)
            WHERE VendorId=vo.VendorId ORDER BY PolicyId DESC) p
        WHERE vo.OrderId=@OrderId;
        IF EXISTS(SELECT 1 FROM dbo.VendorOrders WHERE OrderId=@OrderId AND CommissionPolicyId IS NULL)
            THROW 52406,'Configure commission for every seller before checkout.',1;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
-- Review only: merchandise figures are NOT payout balances.
CREATE OR ALTER VIEW dbo.MarketplaceFinanceReview
AS
SELECT vo.VendorOrderId,vo.OrderId,vo.VendorId,vo.VendorName,vo.MerchandiseSubtotal,
       vo.CommissionRatePercent,vo.CommissionAmount,
       vo.MerchandiseSubtotal-vo.CommissionAmount AS MerchandiseAfterCommission,
       vo.FulfillmentStatus,
       COALESCE(received.Qty,CONVERT(bigint,0)) AS ReceivedReturnQuantity,
       COALESCE(received.Amount,CONVERT(decimal(18,3),0)) AS ReturnedMerchandiseValue,
       CONVERT(decimal(18,3),vo.MerchandiseSubtotal-COALESCE(received.Amount,0)) AS RemainingMerchandiseValue,
       COALESCE(pending.Qty,CONVERT(bigint,0)) AS OpenReturnQuantity,
       CASE WHEN vo.CommissionPolicyId IS NULL THEN 'Missing policy snapshot'
            WHEN EXISTS(SELECT 1 FROM dbo.OrderItems i JOIN dbo.MarketplaceReturnRequests q ON q.OrderItemId=i.OrderItemId
                WHERE i.VendorOrderId=vo.VendorOrderId AND q.Status IN ('Pending','Approved','Received'))
              OR EXISTS(SELECT 1 FROM dbo.OrderItems i JOIN dbo.VendorReturnReceipts rr ON rr.OrderItemId=i.OrderItemId WHERE i.VendorOrderId=vo.VendorOrderId)
              THEN 'Return review required'
            ELSE 'Financial review required' END AS ReviewState
FROM dbo.VendorOrders vo JOIN dbo.Orders o ON o.OrderId=vo.OrderId
JOIN dbo.MarketplacePaymentAttempts p ON p.OrderId=o.OrderId AND p.State='Paid'
OUTER APPLY (
    SELECT SUM(CONVERT(bigint,rr.Quantity)) AS Qty,
           CONVERT(decimal(18,3),SUM(rr.Quantity*i.Price)) AS Amount
    FROM dbo.OrderItems i JOIN dbo.VendorReturnReceipts rr ON rr.OrderItemId=i.OrderItemId
    WHERE i.VendorOrderId=vo.VendorOrderId AND i.OrderId=vo.OrderId
) received
OUTER APPLY (
    SELECT SUM(CONVERT(bigint,q.Quantity)) AS Qty
    FROM dbo.OrderItems i JOIN dbo.MarketplaceReturnRequests q ON q.OrderItemId=i.OrderItemId
    WHERE i.VendorOrderId=vo.VendorOrderId AND i.OrderId=vo.OrderId AND q.Status IN ('Pending','Approved')
) pending
WHERE o.PaymentStatus=1 AND o.MarketplaceRequestId IS NOT NULL;
GO
