:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 52120,'Disposable database required.',1;
GO
:r 09-customer-orders.sql
GO
DECLARE @a int=(SELECT l.VendorListingId FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId WHERE v.VendorCode=N'A');
DECLARE @quote nvarchar(max)=CONCAT('[{"listingId":',@a,',"quantity":1,"price":101.000}]'),@request uniqueidentifier=NEWID();
DECLARE @expiry datetime2(3)=DATEADD(minute,15,SYSUTCDATETIME());
DECLARE @available bigint=(SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE VendorListingId=@a);
INSERT dbo.CartItems(UserId,ProductId,Quantity,Price,VendorListingId) VALUES(1,1,1,101,@a);
BEGIN TRANSACTION;
INSERT dbo.Orders(OrderId,CustomerId,MarketplaceRequestId,TotalAmount) VALUES(10,1,@request,101);
EXEC dbo.AttachMarketplaceOrder 10,1,@quote,@expiry;
COMMIT;
IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceCustomerOrders WHERE OrderId=10 AND CustomerId=1 AND ReservationState='Reserved' AND AttemptState='NotStarted')
    THROW 52121,'New order status incorrect.',1;
BEGIN TRY
    EXEC dbo.CancelUnstartedMarketplaceOrder 10,2;
    THROW 52122,'Other customer cancellation accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51806 THROW;
END CATCH;
EXEC dbo.CancelUnstartedMarketplaceOrder 10,1;
EXEC dbo.CancelUnstartedMarketplaceOrder 10,1;
IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceCustomerOrders WHERE OrderId=10 AND ReservationState='Released')
    OR (SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE VendorListingId=@a)<>@available
    OR (SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE SourceType='Release' AND SourceId=10)<>1
    THROW 52123,'Cancelled order stock state incorrect.',1;
BEGIN TRY
    EXEC dbo.BeginMarketplacePayment 10,1;
    THROW 52124,'Cancelled order allowed to start payment.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51801 THROW;
END CATCH;
IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceCustomerOrders WHERE OrderId=9 AND AttemptState='Paid' AND ReservationState='Consumed')
    OR EXISTS(SELECT 1 FROM dbo.MarketplaceCustomerOrders WHERE OrderId=1)
    THROW 52125,'Paid/legacy order read model incorrect.',1;
PRINT 'Marketplace customer history and safe cancellation checks passed.';
GO
