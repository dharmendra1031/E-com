SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
-- Gateway metadata only; Payments remains the financial record.
IF OBJECT_ID(N'dbo.MarketplacePaymentAttempts',N'U') IS NULL
CREATE TABLE dbo.MarketplacePaymentAttempts (
    OrderId int NOT NULL PRIMARY KEY REFERENCES dbo.Orders(OrderId),
    MerchantReference uniqueidentifier NOT NULL UNIQUE,
    InvoiceId bigint NULL,
    PaymentId nvarchar(100) NULL,
    ExpectedAmount decimal(18,3) NOT NULL CHECK(ExpectedAmount>0),
    Currency char(3) NOT NULL CHECK(Currency='KWD'),
    State varchar(16) NOT NULL CHECK(State IN ('Starting','Linked','Paid','Released')),
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MarketplacePaymentAttempts') AND name=N'UX_MarketplacePayment_Invoice')
    CREATE UNIQUE INDEX UX_MarketplacePayment_Invoice ON dbo.MarketplacePaymentAttempts(InvoiceId) WHERE InvoiceId IS NOT NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MarketplacePaymentAttempts') AND name=N'UX_MarketplacePayment_Payment')
    CREATE UNIQUE INDEX UX_MarketplacePayment_Payment ON dbo.MarketplacePaymentAttempts(PaymentId) WHERE PaymentId IS NOT NULL;
GO
CREATE OR ALTER PROCEDURE dbo.BeginMarketplacePayment
    @OrderId int, @CustomerId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET ANSI_PADDING ON;
    SET ANSI_WARNINGS ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @total decimal(18,3), @ref uniqueidentifier;
        SELECT @total=TotalAmount,@ref=MarketplaceRequestId FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK)
        WHERE OrderId=@OrderId AND CustomerId=@CustomerId AND MarketplaceRequestId IS NOT NULL AND PaymentStatus=2;
        IF @ref IS NULL OR @total<=0 THROW 51800,'Unpaid marketplace order required.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.InventoryReservations WHERE OrderId=@OrderId)
           OR EXISTS(SELECT 1 FROM dbo.InventoryReservations WHERE OrderId=@OrderId AND (Status<>'Reserved' OR ExpiresAtUtc<=SYSUTCDATETIME()))
            THROW 51801,'Reservation not active.',1;
        -- Do not automatically create another invoice when a network response was lost.
        IF EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WHERE OrderId=@OrderId)
            THROW 51802,'Existing payment attempt requires reconciliation.',1;
        INSERT dbo.MarketplacePaymentAttempts(OrderId,MerchantReference,ExpectedAmount,Currency,State)
        VALUES(@OrderId,@ref,@total,'KWD','Starting');
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.LinkMarketplaceInvoice
    @OrderId int,@InvoiceId bigint,@MerchantReference uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @InvoiceId IS NULL OR @InvoiceId<=0 THROW 51803,'Invalid invoice.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@OrderId AND MarketplaceRequestId=@MerchantReference)
            THROW 51803,'Order reference mismatch.',1;
        IF EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WITH(UPDLOCK,HOLDLOCK) WHERE InvoiceId=@InvoiceId AND OrderId<>@OrderId)
            THROW 51803,'Invoice already linked.',1;
        UPDATE dbo.MarketplacePaymentAttempts SET InvoiceId=@InvoiceId,State='Linked',UpdatedAtUtc=SYSUTCDATETIME()
        WHERE OrderId=@OrderId AND MerchantReference=@MerchantReference AND State='Starting' AND InvoiceId IS NULL;
        IF @@ROWCOUNT<>1 AND NOT EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WHERE OrderId=@OrderId AND InvoiceId=@InvoiceId AND MerchantReference=@MerchantReference AND State='Linked')
            THROW 51803,'Invoice linkage conflict.',1;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
-- Restricted internal caller ONLY, after server-to-server MyFatoorah verification.
-- The supplied evidence fields are not proof by themselves.
CREATE OR ALTER PROCEDURE dbo.ConfirmMarketplacePayment
    @OrderId int,@InvoiceId bigint,@MerchantReference uniqueidentifier,
    @PaymentId nvarchar(100),@Amount decimal(18,3),@Currency char(3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET ANSI_PADDING ON;
    SET ANSI_WARNINGS ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;
    IF @PaymentId IS NULL OR LEN(@PaymentId)=0 OR @Amount IS NULL OR @Currency IS NULL OR @Currency<>'KWD'
        THROW 51804,'Invalid evidence.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@OrderId
                      AND MarketplaceRequestId=@MerchantReference AND TotalAmount=@Amount)
            THROW 51804,'Order or amount mismatch.',1;
        IF EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WITH(UPDLOCK,HOLDLOCK) WHERE PaymentId=@PaymentId AND OrderId<>@OrderId)
            THROW 51804,'Payment already linked to another order.',1;
        IF EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WHERE OrderId=@OrderId AND InvoiceId=@InvoiceId
            AND MerchantReference=@MerchantReference AND ExpectedAmount=@Amount AND Currency=@Currency AND State='Paid' AND PaymentId=@PaymentId)
        BEGIN
            IF NOT EXISTS(SELECT 1 FROM dbo.Orders WHERE OrderId=@OrderId AND PaymentStatus=1)
                THROW 51805,'Paid state inconsistent.',1;
            COMMIT TRANSACTION;
            RETURN;
        END;
        IF NOT EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WHERE OrderId=@OrderId AND InvoiceId=@InvoiceId
            AND MerchantReference=@MerchantReference AND ExpectedAmount=@Amount AND Currency=@Currency AND State='Linked')
            THROW 51804,'Unmatched or finalized invoice.',1;
        IF EXISTS(SELECT 1 FROM dbo.Payments WHERE OrderId=@OrderId)
            THROW 51805,'Existing payment requires reconciliation.',1;
        EXEC dbo.FinalizeOrderInventory @OrderId,'Consumed','PaymentConfirmed';
        UPDATE dbo.Orders SET PaymentStatus=1,OrderTransaction=CONVERT(nvarchar(30),@InvoiceId),UpdatedAt=GETDATE()
        WHERE OrderId=@OrderId AND PaymentStatus=2;
        IF @@ROWCOUNT<>1 THROW 51805,'Order payment state changed.',1;
        INSERT dbo.Payments(OrderId,AmountPaid,PaymentMethod,PaymentStatus,TransactionId,PaidAt)
        VALUES(@OrderId,@Amount,'Online',1,CONVERT(nvarchar(30),@InvoiceId),GETDATE());
        UPDATE dbo.MarketplacePaymentAttempts SET State='Paid',PaymentId=@PaymentId,UpdatedAtUtc=SYSUTCDATETIME() WHERE OrderId=@OrderId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
-- Only release orders that NEVER began payment. Linked/uncertain invoices require gateway reconciliation.
CREATE OR ALTER PROCEDURE dbo.CancelUnstartedMarketplaceOrder
    @OrderId int,@CustomerId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET ANSI_PADDING ON;
    SET ANSI_WARNINGS ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@OrderId AND CustomerId=@CustomerId AND MarketplaceRequestId IS NOT NULL AND PaymentStatus=2)
            THROW 51806,'Unpaid order required.',1;
        IF EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WHERE OrderId=@OrderId)
            THROW 51806,'Payment attempt requires reconciliation.',1;
        EXEC dbo.FinalizeOrderInventory @OrderId,'Released','UnpaidCancellation';
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
