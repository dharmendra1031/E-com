SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO
-- One immutable amount and one gateway request identity per accepted return.
IF OBJECT_ID(N'dbo.MarketplaceRefundAttempts',N'U') IS NULL
CREATE TABLE dbo.MarketplaceRefundAttempts(
    ReturnId int NOT NULL PRIMARY KEY REFERENCES dbo.MarketplaceReturnRequests(ReturnId),
    OrderId int NOT NULL REFERENCES dbo.Orders(OrderId),
    InvoiceId bigint NOT NULL CHECK(InvoiceId>0),
    MerchantReference uniqueidentifier NOT NULL UNIQUE,
    Amount decimal(18,3) NOT NULL CHECK(Amount>0),
    RequestedBy int NOT NULL REFERENCES dbo.Users(UserId),
    RequestedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    GatewayRefundId bigint NULL CHECK(GatewayRefundId>0),
    VerifiedBy int NULL REFERENCES dbo.Users(UserId),
    VerifiedAtUtc datetime2(3) NULL,
    CONSTRAINT CK_MarketplaceRefund_Verified CHECK(
        (GatewayRefundId IS NULL AND VerifiedBy IS NULL AND VerifiedAtUtc IS NULL)
        OR (GatewayRefundId IS NOT NULL AND VerifiedBy IS NOT NULL AND VerifiedAtUtc IS NOT NULL))
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MarketplaceRefundAttempts') AND name='UX_MarketplaceRefund_Gateway')
    CREATE UNIQUE INDEX UX_MarketplaceRefund_Gateway ON dbo.MarketplaceRefundAttempts(GatewayRefundId) WHERE GatewayRefundId IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MarketplaceRefundAttempts') AND name='IX_MarketplaceRefund_Order')
    CREATE INDEX IX_MarketplaceRefund_Order ON dbo.MarketplaceRefundAttempts(OrderId) INCLUDE(Amount);
GO
CREATE OR ALTER PROCEDURE dbo.BeginMarketplaceRefund
    @ReturnId int,@ActorId int,@Reference uniqueidentifier,@Amount decimal(18,3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @Reference IS NULL OR @Reference='00000000-0000-0000-0000-000000000000' OR @Amount IS NULL OR @Amount<=0
        THROW 52600,'Invalid refund request.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Users u WITH(HOLDLOCK) JOIN dbo.[Role] r WITH(HOLDLOCK) ON r.RoleId=u.RoleId
            WHERE u.UserId=@ActorId AND u.IsActive=1 AND r.IsActive=1 AND r.RoleName='Admin')
            THROW 52601,'Admin required.',1;
        DECLARE @order int,@invoice bigint,@cap decimal(18,3),@total decimal(18,3);
        SELECT @order=OrderId FROM dbo.MarketplaceRefundReview WHERE ReturnId=@ReturnId;
        SELECT @total=TotalAmount FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK)
            WHERE OrderId=@order AND MarketplaceRequestId IS NOT NULL AND PaymentStatus=1;
        SELECT @invoice=InvoiceId,@cap=ReturnedMerchandiseValue FROM dbo.MarketplaceRefundReview
            WHERE ReturnId=@ReturnId AND OrderId=@order;
        IF @total IS NULL OR @invoice IS NULL OR @cap IS NULL OR @Amount>@cap OR
           NOT EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WITH(HOLDLOCK)
             WHERE OrderId=@order AND InvoiceId=@invoice AND ExpectedAmount=@total AND Currency='KWD' AND State='Paid')
            THROW 52602,'Refund evidence or amount invalid.',1;
        IF EXISTS(SELECT 1 FROM dbo.MarketplaceRefundAttempts WITH(UPDLOCK,HOLDLOCK) WHERE ReturnId=@ReturnId)
            THROW 52603,'Existing refund requires reconciliation.',1;
        IF EXISTS(SELECT 1 FROM dbo.MarketplaceRefundAttempts WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@order AND GatewayRefundId IS NULL)
            THROW 52603,'Earlier invoice refund requires reconciliation.',1;
        -- Include uncertain requests in reserved refund total.
        IF @Amount+(SELECT COALESCE(SUM(Amount),0) FROM dbo.MarketplaceRefundAttempts WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@order)>@total
            THROW 52604,'Refund exceeds original payment.',1;
        INSERT dbo.MarketplaceRefundAttempts(ReturnId,OrderId,InvoiceId,MerchantReference,Amount,RequestedBy)
        VALUES(@ReturnId,@order,@invoice,@Reference,@Amount,@ActorId);
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
-- Trusted gateway reconciliation caller only; no direct operator status entry.
CREATE OR ALTER PROCEDURE dbo.ConfirmMarketplaceRefund
    @ReturnId int,@ActorId int,@Reference uniqueidentifier,@InvoiceId bigint,@Amount decimal(18,3),@GatewayRefundId bigint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @GatewayRefundId IS NULL OR @GatewayRefundId<=0 THROW 52605,'Invalid refund proof.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Users u WITH(HOLDLOCK) JOIN dbo.[Role] r WITH(HOLDLOCK) ON r.RoleId=u.RoleId
            WHERE u.UserId=@ActorId AND u.IsActive=1 AND r.IsActive=1 AND r.RoleName='Admin')
            THROW 52601,'Admin required.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceRefundAttempts WITH(UPDLOCK,HOLDLOCK)
            WHERE ReturnId=@ReturnId AND MerchantReference=@Reference AND InvoiceId=@InvoiceId AND Amount=@Amount
                AND (GatewayRefundId IS NULL OR GatewayRefundId=@GatewayRefundId))
            THROW 52605,'Refund proof mismatch.',1;
        IF EXISTS(SELECT 1 FROM dbo.MarketplaceRefundAttempts WITH(UPDLOCK,HOLDLOCK)
            WHERE GatewayRefundId=@GatewayRefundId AND ReturnId<>@ReturnId)
            THROW 52605,'Refund already linked.',1;
        UPDATE dbo.MarketplaceRefundAttempts SET GatewayRefundId=@GatewayRefundId,VerifiedBy=@ActorId,VerifiedAtUtc=SYSUTCDATETIME()
        WHERE ReturnId=@ReturnId AND GatewayRefundId IS NULL;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
