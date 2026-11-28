SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
-- Line-level marketplace return authorization, distinct from actual receipt and refund.
IF OBJECT_ID(N'dbo.MarketplaceReturnRequests',N'U') IS NULL
CREATE TABLE dbo.MarketplaceReturnRequests(
    ReturnId int IDENTITY PRIMARY KEY,
    RequestId uniqueidentifier NOT NULL UNIQUE,
    OrderItemId int NOT NULL REFERENCES dbo.OrderItems(OrderItemId),
    CustomerId int NOT NULL,
    Quantity int NOT NULL CHECK(Quantity>0),
    Reason nvarchar(200) NOT NULL,
    Status varchar(12) NOT NULL CHECK(Status IN ('Pending','Approved','Rejected','Received')),
    RequestedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    ReviewedBy int NULL REFERENCES dbo.Users(UserId),
    ReviewedAtUtc datetime2(3) NULL,
    ReceivedBy int NULL REFERENCES dbo.Users(UserId),
    ReceivedAtUtc datetime2(3) NULL
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MarketplaceReturnRequests') AND name='IX_MarketplaceReturns_Item')
    CREATE INDEX IX_MarketplaceReturns_Item ON dbo.MarketplaceReturnRequests(OrderItemId,Status) INCLUDE(Quantity);
GO
CREATE OR ALTER PROCEDURE dbo.RequestMarketplaceReturn
    @CustomerId int,@OrderItemId int,@RequestId uniqueidentifier,@Quantity int,@Reason nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @RequestId IS NULL OR @RequestId='00000000-0000-0000-0000-000000000000' OR @Quantity IS NULL OR @Quantity<=0
       OR @Reason IS NULL OR LEN(LTRIM(RTRIM(@Reason)))=0 THROW 52300,'Invalid return request.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @order int,@sold int;
        SELECT @order=i.OrderId FROM dbo.OrderItems i WHERE i.OrderItemId=@OrderItemId AND i.VendorListingId IS NOT NULL;
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@order AND CustomerId=@CustomerId AND MarketplaceRequestId IS NOT NULL AND PaymentStatus=1)
           OR NOT EXISTS(SELECT 1 FROM dbo.VerifiedMarketplacePaidOrders WHERE OrderId=@order)
            THROW 52301,'Paid owned order required.',1;
        SELECT @sold=i.Quantity FROM dbo.OrderItems i WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.InventoryReservations r ON r.OrderId=i.OrderId AND r.VendorListingId=i.VendorListingId
            WHERE i.OrderItemId=@OrderItemId AND r.Status='Consumed' AND r.Quantity=i.Quantity;
        IF @sold IS NULL THROW 52301,'Consumed item required.',1;
        IF EXISTS(SELECT 1 FROM dbo.MarketplaceReturnRequests WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@RequestId)
        BEGIN
            IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceReturnRequests WHERE RequestId=@RequestId AND CustomerId=@CustomerId
                AND OrderItemId=@OrderItemId AND Quantity=@Quantity AND CONVERT(varbinary(400),Reason)=CONVERT(varbinary(400),@Reason))
                THROW 52302,'Changed request.',1;
            COMMIT;
            RETURN;
        END;
        DECLARE @used bigint=(SELECT COALESCE(SUM(CONVERT(bigint,Quantity)),0) FROM dbo.VendorReturnReceipts WITH(UPDLOCK,HOLDLOCK) WHERE OrderItemId=@OrderItemId)
           +(SELECT COALESCE(SUM(CONVERT(bigint,Quantity)),0) FROM dbo.MarketplaceReturnRequests WITH(UPDLOCK,HOLDLOCK)
              WHERE OrderItemId=@OrderItemId AND Status IN ('Pending','Approved'));
        IF @used+@Quantity>@sold THROW 52303,'Requested return exceeds remaining quantity.',1;
        INSERT dbo.MarketplaceReturnRequests(RequestId,OrderItemId,CustomerId,Quantity,Reason,Status)
        VALUES(@RequestId,@OrderItemId,@CustomerId,@Quantity,@Reason,'Pending');
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
-- App superadmin policy required in addition to active Admin actor check.
CREATE OR ALTER PROCEDURE dbo.ReviewMarketplaceReturn
    @ReturnId int,@ActorId int,@ExpectedStatus varchar(12),@NewStatus varchar(12)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @ExpectedStatus IS NULL OR @NewStatus IS NULL OR NOT (
       (@ExpectedStatus='Pending' AND @NewStatus IN ('Approved','Rejected')) OR
       (@ExpectedStatus='Approved' AND @NewStatus='Received'))
       THROW 52304,'Invalid return transition.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Users u WITH(HOLDLOCK) JOIN dbo.[Role] r WITH(HOLDLOCK) ON r.RoleId=u.RoleId
            WHERE u.UserId=@ActorId AND u.IsActive=1 AND r.IsActive=1 AND r.RoleName='Admin')
            THROW 52305,'Admin permission required.',1;
        DECLARE @order int,@item int,@request uniqueidentifier,@qty int,@reason nvarchar(200),@status varchar(12);
        SELECT @order=i.OrderId FROM dbo.MarketplaceReturnRequests q JOIN dbo.OrderItems i ON i.OrderItemId=q.OrderItemId WHERE q.ReturnId=@ReturnId;
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@order AND PaymentStatus=1 AND MarketplaceRequestId IS NOT NULL)
           OR NOT EXISTS(SELECT 1 FROM dbo.VerifiedMarketplacePaidOrders WHERE OrderId=@order)
            THROW 52306,'Paid order required.',1;
        SELECT @item=OrderItemId,@request=RequestId,@qty=Quantity,@reason=Reason,@status=Status
        FROM dbo.MarketplaceReturnRequests WITH(UPDLOCK,HOLDLOCK) WHERE ReturnId=@ReturnId;
        IF @status=@NewStatus
        BEGIN
            IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceReturnRequests WHERE ReturnId=@ReturnId AND
                ((@NewStatus='Received' AND ReceivedBy=@ActorId) OR (@NewStatus IN ('Approved','Rejected') AND ReviewedBy=@ActorId)))
                THROW 52306,'Return status changed.',1;
            COMMIT;
            RETURN;
        END;
        IF @status IS NULL OR @status<>@ExpectedStatus THROW 52306,'Return status changed.',1;
        IF @NewStatus='Received'
        BEGIN
            EXEC dbo.PostMarketplaceReturnStock @item,@request,@qty,@reason,@ActorId;
            UPDATE dbo.MarketplaceReturnRequests SET Status='Received',ReceivedBy=@ActorId,ReceivedAtUtc=SYSUTCDATETIME() WHERE ReturnId=@ReturnId;
        END
        ELSE
            UPDATE dbo.MarketplaceReturnRequests SET Status=@NewStatus,ReviewedBy=@ActorId,ReviewedAtUtc=SYSUTCDATETIME() WHERE ReturnId=@ReturnId;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER VIEW dbo.MarketplaceReturnItems
AS
SELECT i.OrderItemId,o.CustomerId,o.MarketplaceRequestId AS OrderRequestId,i.ProductNameSnapshot AS ProductName,
       i.ProductNameArbSnapshot AS ProductNameArb,i.Quantity AS PurchasedQuantity,
       CONVERT(int,i.Quantity-COALESCE(r.Qty,0)-COALESCE(q.Qty,0)) AS RemainingQuantity
FROM dbo.OrderItems i JOIN dbo.Orders o ON o.OrderId=i.OrderId
JOIN dbo.VerifiedMarketplacePaidOrders p ON p.OrderId=o.OrderId
JOIN dbo.InventoryReservations ir ON ir.OrderId=i.OrderId AND ir.VendorListingId=i.VendorListingId AND ir.Status='Consumed' AND ir.Quantity=i.Quantity
OUTER APPLY(SELECT SUM(CONVERT(bigint,Quantity)) Qty FROM dbo.VendorReturnReceipts WHERE OrderItemId=i.OrderItemId) r
OUTER APPLY(SELECT SUM(CONVERT(bigint,Quantity)) Qty FROM dbo.MarketplaceReturnRequests WHERE OrderItemId=i.OrderItemId AND Status IN ('Pending','Approved')) q
WHERE o.PaymentStatus=1 AND o.MarketplaceRequestId IS NOT NULL;
GO
CREATE OR ALTER VIEW dbo.MarketplaceReturnReview
AS
SELECT q.ReturnId,q.CustomerId,q.OrderItemId,o.OrderId,o.MarketplaceRequestId AS OrderRequestId,
       i.ProductNameSnapshot AS ProductName,i.ProductNameArbSnapshot AS ProductNameArb,
       q.Quantity,q.Reason,q.Status,q.RequestedAtUtc
FROM dbo.MarketplaceReturnRequests q JOIN dbo.OrderItems i ON i.OrderItemId=q.OrderItemId
JOIN dbo.Orders o ON o.OrderId=i.OrderId;
GO
