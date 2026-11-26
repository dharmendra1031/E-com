SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'dbo.VendorOrders',N'FulfillmentStatus') IS NULL
    ALTER TABLE dbo.VendorOrders ADD FulfillmentStatus varchar(20) NOT NULL CONSTRAINT DF_VendorOrders_Fulfillment DEFAULT 'AwaitingPacking',
        FulfillmentUpdatedAtUtc datetime2(3) NULL;
GO
IF OBJECT_ID(N'dbo.CK_VendorOrders_Fulfillment',N'C') IS NULL
    ALTER TABLE dbo.VendorOrders WITH CHECK ADD CONSTRAINT CK_VendorOrders_Fulfillment
        CHECK(FulfillmentStatus IN ('AwaitingPacking','Packed','HandedOver','Delivered'));
IF OBJECT_ID(N'dbo.VendorOrderFulfillmentHistory',N'U') IS NULL
    CREATE TABLE dbo.VendorOrderFulfillmentHistory (
        HistoryId bigint IDENTITY PRIMARY KEY,
        VendorOrderId int NOT NULL REFERENCES dbo.VendorOrders(VendorOrderId),
        PreviousStatus varchar(20) NOT NULL,
        NewStatus varchar(20) NOT NULL,
        ChangedBy int NOT NULL REFERENCES dbo.Users(UserId),
        ChangedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_VendorOrderFulfillment_Step UNIQUE(VendorOrderId,NewStatus)
    );
GO
CREATE OR ALTER PROCEDURE dbo.AdvanceVendorFulfillment
    @VendorId int,@VendorOrderId int,@ActorId int,
    @ExpectedStatus varchar(20),@NewStatus varchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @ExpectedStatus IS NULL OR @NewStatus IS NULL OR NOT (
        (@ExpectedStatus='AwaitingPacking' AND @NewStatus='Packed') OR
        (@ExpectedStatus='Packed' AND @NewStatus='HandedOver'))
        THROW 52000,'Invalid fulfillment transition.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.VendorUsers m WITH(HOLDLOCK)
            JOIN dbo.Users u WITH(HOLDLOCK) ON u.UserId=m.UserId
            JOIN dbo.[Role] r WITH(HOLDLOCK) ON r.RoleId=u.RoleId
            JOIN dbo.Vendors v WITH(HOLDLOCK) ON v.VendorId=m.VendorId
            WHERE m.UserId=@ActorId AND m.VendorId=@VendorId AND m.IsActive=1 AND u.IsActive=1
              AND r.IsActive=1 AND r.RoleName IN ('VendorOwner','VendorManager') AND v.Status='Approved')
            THROW 52001,'Vendor permission denied.',1;
        DECLARE @orderId int=(SELECT OrderId FROM dbo.VendorOrders WHERE VendorOrderId=@VendorOrderId AND VendorId=@VendorId);
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@orderId AND PaymentStatus=1 AND MarketplaceRequestId IS NOT NULL)
           OR NOT EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WHERE OrderId=@orderId AND State='Paid')
            THROW 52002,'Paid vendor order required.',1;
        UPDATE dbo.VendorOrders SET FulfillmentStatus=@NewStatus,FulfillmentUpdatedAtUtc=SYSUTCDATETIME()
        WHERE VendorOrderId=@VendorOrderId AND VendorId=@VendorId AND FulfillmentStatus=@ExpectedStatus;
        IF @@ROWCOUNT<>1
        BEGIN
            IF EXISTS(SELECT 1 FROM dbo.VendorOrders v JOIN dbo.VendorOrderFulfillmentHistory h ON h.VendorOrderId=v.VendorOrderId
                WHERE v.VendorOrderId=@VendorOrderId AND v.VendorId=@VendorId AND v.FulfillmentStatus=@NewStatus
                  AND h.PreviousStatus=@ExpectedStatus AND h.NewStatus=@NewStatus AND h.ChangedBy=@ActorId)
            BEGIN
                COMMIT;
                RETURN;
            END;
            THROW 52003,'Order status changed.',1;
        END;
        INSERT dbo.VendorOrderFulfillmentHistory(VendorOrderId,PreviousStatus,NewStatus,ChangedBy)
        VALUES(@VendorOrderId,@ExpectedStatus,@NewStatus,@ActorId);
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
