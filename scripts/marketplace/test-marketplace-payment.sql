:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 51820,'Disposable database required.',1;
GO
ALTER TABLE dbo.Orders ADD OrderTransaction nvarchar(100) NULL,UpdatedAt datetime NULL;
CREATE TABLE dbo.Payments(PaymentId int IDENTITY PRIMARY KEY,OrderId int NOT NULL,AmountPaid decimal(18,3),PaymentMethod nvarchar(50),PaymentStatus int,TransactionId nvarchar(100),PaidAt datetime);
GO
:r 06-marketplace-payment.sql
GO
DECLARE @a int=(SELECT l.VendorListingId FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId WHERE v.VendorCode=N'A'),
        @b int=(SELECT l.VendorListingId FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId WHERE v.VendorCode=N'B');
DECLARE @quote nvarchar(max)=CONCAT('[{"listingId":',@a,',"quantity":1,"price":101.000},{"listingId":',@b,',"quantity":2,"price":95.000}]');
DECLARE @expiry datetime2(3)=DATEADD(minute,15,SYSUTCDATETIME()),@request uniqueidentifier=NEWID();
BEGIN TRANSACTION;
INSERT dbo.Orders(OrderId,CustomerId,MarketplaceRequestId,TotalAmount) VALUES(9,1,@request,291);
EXEC dbo.AttachMarketplaceOrder 9,1,@quote,@expiry;
COMMIT;
EXEC dbo.BeginMarketplacePayment 9,1;
BEGIN TRY
    EXEC dbo.BeginMarketplacePayment 9,1;
    THROW 51821,'Duplicate attempt accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51802 THROW;
END CATCH;
EXEC dbo.LinkMarketplaceInvoice 9,12345,@request;
EXEC dbo.LinkMarketplaceInvoice 9,12345,@request;
BEGIN TRY
    EXEC dbo.ConfirmMarketplacePayment 9,12345,@request,N'PAY1',290,'KWD';
    THROW 51822,'Wrong amount accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51804 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.CancelUnstartedMarketplaceOrder 9,1;
    THROW 51823,'Linked invoice cancelled without verification.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51806 THROW;
END CATCH;
EXEC dbo.ConfirmMarketplacePayment 9,12345,@request,N'PAY1',291,'KWD';
EXEC dbo.ConfirmMarketplacePayment 9,12345,@request,N'PAY1',291,'KWD';
IF (SELECT COUNT(*) FROM dbo.Payments WHERE OrderId=9 AND AmountPaid=291 AND PaymentStatus=1)<>1
   OR NOT EXISTS(SELECT 1 FROM dbo.Orders WHERE OrderId=9 AND PaymentStatus=1)
   OR EXISTS(SELECT 1 FROM dbo.InventoryReservations WHERE OrderId=9 AND Status<>'Consumed')
   OR (SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE SourceType='Consume' AND SourceId=9)<>2
    THROW 51824,'Paid inventory/ledger or retry incorrect.',1;
PRINT 'Marketplace payment posting foundation checks passed.';
GO
