SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
-- Physical restock evidence only. No refund or settlement transaction is implied.
IF OBJECT_ID(N'dbo.VendorReturnReceipts',N'U') IS NULL
CREATE TABLE dbo.VendorReturnReceipts (
    ReturnReceiptId bigint IDENTITY PRIMARY KEY,
    OrderItemId int NOT NULL REFERENCES dbo.OrderItems(OrderItemId),
    ReceiptRequestId uniqueidentifier NOT NULL UNIQUE,
    Quantity int NOT NULL CHECK(Quantity>0),
    Reason nvarchar(200) NOT NULL,
    AcceptedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.VendorReturnReceipts') AND name=N'IX_VendorReturnReceipts_Item')
    CREATE INDEX IX_VendorReturnReceipts_Item ON dbo.VendorReturnReceipts(OrderItemId) INCLUDE(Quantity);
GO
-- Trusted internal service only after authorized physical acceptance; no public caller.
CREATE OR ALTER PROCEDURE dbo.PostMarketplaceReturnStock
    @OrderItemId int, @ReceiptRequestId uniqueidentifier, @Quantity int, @Reason nvarchar(200), @ActorId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET ANSI_PADDING ON;
    SET ANSI_WARNINGS ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;
    IF @ReceiptRequestId IS NULL OR @ReceiptRequestId='00000000-0000-0000-0000-000000000000'
        OR @Quantity IS NULL OR @Quantity<=0 OR @Reason IS NULL OR LEN(LTRIM(RTRIM(@Reason)))=0
        THROW 51900,'Invalid accepted return.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @listing int,@sold int,@order int;
        SELECT @order=OrderId FROM dbo.OrderItems WHERE OrderItemId=@OrderItemId AND VendorListingId IS NOT NULL;
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@order AND MarketplaceRequestId IS NOT NULL AND PaymentStatus=1)
            THROW 51901,'Paid marketplace order required.',1;
        SELECT @listing=VendorListingId,@sold=Quantity FROM dbo.OrderItems WITH(UPDLOCK,HOLDLOCK)
            WHERE OrderItemId=@OrderItemId AND OrderId=@order;
        IF @listing IS NULL OR @sold IS NULL OR @sold<=0
           OR NOT EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WHERE OrderId=@order AND State='Paid')
           OR NOT EXISTS(SELECT 1 FROM dbo.InventoryReservations WHERE OrderId=@order AND VendorListingId=@listing AND Status='Consumed' AND Quantity=@sold)
            THROW 51901,'Consumed listing required.',1;
        IF EXISTS(SELECT 1 FROM dbo.VendorReturnReceipts WITH(UPDLOCK,HOLDLOCK) WHERE ReceiptRequestId=@ReceiptRequestId)
        BEGIN
            IF NOT EXISTS(SELECT 1 FROM dbo.VendorReturnReceipts WHERE ReceiptRequestId=@ReceiptRequestId
              AND OrderItemId=@OrderItemId AND Quantity=@Quantity AND CONVERT(varbinary(400),Reason)=CONVERT(varbinary(400),@Reason))
                THROW 51902,'Return request differs.',1;
            COMMIT;
            RETURN;
        END;
        DECLARE @returned bigint=(SELECT COALESCE(SUM(CONVERT(bigint,Quantity)),0) FROM dbo.VendorReturnReceipts WITH(UPDLOCK,HOLDLOCK) WHERE OrderItemId=@OrderItemId);
        IF @returned+@Quantity>@sold THROW 51903,'Return exceeds purchased quantity.',1;
        INSERT dbo.VendorReturnReceipts(OrderItemId,ReceiptRequestId,Quantity,Reason) VALUES(@OrderItemId,@ReceiptRequestId,@Quantity,@Reason);
        DECLARE @receipt bigint=CONVERT(bigint,SCOPE_IDENTITY());
        UPDATE dbo.InventoryBalances SET OnHandQuantity=OnHandQuantity+@Quantity,UpdatedAtUtc=SYSUTCDATETIME()
            WHERE VendorListingId=@listing AND OnHandQuantity<=9223372036854775807-@Quantity;
        IF @@ROWCOUNT<>1 THROW 51904,'Inventory balance unavailable.',1;
        INSERT dbo.InventoryTransactions(VendorListingId,OnHandDelta,ReservedDelta,SourceType,SourceId,SourceLineId,Reason,CreatedBy)
            VALUES(@listing,@Quantity,0,'ReturnReceipt',@receipt,@OrderItemId,@Reason,@ActorId);
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
