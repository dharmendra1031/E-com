:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 51720,'Disposable database required.',1;
GO
ALTER TABLE dbo.Orders ADD CustomerId int NOT NULL DEFAULT(1), Status int NOT NULL DEFAULT(1),
    PaymentStatus int NOT NULL DEFAULT(2), TotalAmount decimal(18,3) NOT NULL DEFAULT(0),
    VatAmount decimal(18,3) NOT NULL DEFAULT(0), VatPercent decimal(18,3) NOT NULL DEFAULT(0),
    DiscountAmount decimal(18,3) NULL, DeliveryCharge decimal(18,3) NOT NULL DEFAULT(0);
ALTER TABLE dbo.Products ADD Name nvarchar(max) NOT NULL DEFAULT(N'Product'), NameArb nvarchar(max) NULL;
CREATE TABLE dbo.OrderItems(OrderItemId int IDENTITY PRIMARY KEY,OrderId int NULL,ProductId int NULL,Quantity int NULL,Price decimal(18,3) NULL);
GO
:r 04-vendor-orders.sql
GO
:r 04-vendor-orders.sql
GO
:r 05-attach-marketplace-order.sql
GO
SET XACT_ABORT OFF;
DECLARE @a int=(SELECT l.VendorListingId FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId WHERE v.VendorCode=N'A'),
        @b int=(SELECT l.VendorListingId FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId WHERE v.VendorCode=N'B');
DECLARE @quote nvarchar(max)=CONCAT('[{"listingId":',@a,',"quantity":1,"price":101.000},{"listingId":',@b,',"quantity":2,"price":95.000}]');
DECLARE @expiry datetime2(3)=DATEADD(minute,15,SYSUTCDATETIME()), @request uniqueidentifier=NEWID();
BEGIN TRANSACTION;
INSERT dbo.Orders(OrderId,CustomerId,MarketplaceRequestId,TotalAmount,DeliveryCharge) VALUES(9,1,@request,294,3);
EXEC dbo.AttachMarketplaceOrder 9,1,@quote,@expiry;
IF (SELECT COUNT(*) FROM dbo.VendorOrders WHERE OrderId=9)<>2
   OR (SELECT SUM(MerchandiseSubtotal) FROM dbo.VendorOrders WHERE OrderId=9)<>291
   OR (SELECT COUNT(*) FROM dbo.OrderItems WHERE OrderId=9 AND SellerSkuSnapshot IS NOT NULL)<>2
   OR (SELECT COUNT(*) FROM dbo.InventoryReservations WHERE OrderId=9 AND Status='Reserved')<>2
   OR EXISTS(SELECT 1 FROM dbo.CartItems WHERE UserId=1 AND VendorListingId IS NOT NULL)
   OR (SELECT COUNT(*) FROM dbo.CartItems WHERE VendorListingId IS NULL)<>2
   THROW 51721,'Order split, reservation or cart clear incorrect.',1;
ROLLBACK TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.Orders WHERE OrderId=9) OR EXISTS(SELECT 1 FROM dbo.VendorOrders WHERE OrderId=9)
   OR EXISTS(SELECT 1 FROM dbo.InventoryReservations WHERE OrderId=9) OR (SELECT COUNT(*) FROM dbo.CartItems WHERE VendorListingId IS NOT NULL)<>2
   THROW 51722,'Outer rollback incomplete.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    INSERT dbo.Orders(OrderId,CustomerId,MarketplaceRequestId,TotalAmount) VALUES(9,1,@request,1);
    EXEC dbo.AttachMarketplaceOrder 9,1,@quote,@expiry;
    THROW 51723,'Incorrect total accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51716 THROW;
END CATCH;
IF EXISTS(SELECT 1 FROM dbo.Orders WHERE OrderId=9) THROW 51724,'Failed parent persisted.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE dbo.VendorListings SET Price=102 WHERE VendorListingId=@a;
    INSERT dbo.Orders(OrderId,CustomerId,MarketplaceRequestId,TotalAmount) VALUES(9,1,@request,291);
    EXEC dbo.AttachMarketplaceOrder 9,1,@quote,@expiry;
    THROW 51725,'Changed price accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51715 THROW;
END CATCH;
BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE dbo.InventoryBalances SET ReservedQuantity=OnHandQuantity WHERE VendorListingId=@b;
    INSERT dbo.Orders(OrderId,CustomerId,MarketplaceRequestId,TotalAmount) VALUES(9,1,@request,291);
    EXEC dbo.AttachMarketplaceOrder 9,1,@quote,@expiry;
    THROW 51726,'Insufficient stock accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51404 THROW;
END CATCH;
IF EXISTS(SELECT 1 FROM dbo.Orders WHERE OrderId=9) OR EXISTS(SELECT 1 FROM dbo.VendorOrders WHERE OrderId=9)
   OR EXISTS(SELECT 1 FROM dbo.InventoryReservations WHERE OrderId=9) OR (SELECT COUNT(*) FROM dbo.CartItems WHERE VendorListingId IS NOT NULL)<>2
    THROW 51727,'Failed basket rollback incomplete.',1;
PRINT 'Marketplace order attachment checks passed.';
GO
