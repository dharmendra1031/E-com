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
-- A webhook can complete payment before the original SendPayment caller links its invoice.
EXEC dbo.LinkMarketplaceInvoice 9,12345,@request;
BEGIN TRY
    EXEC dbo.LinkMarketplaceInvoice 9,12346,@request;
    THROW 51828,'Paid invoice linkage changed.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51803 THROW;
END CATCH;
EXEC dbo.ConfirmMarketplacePayment 9,12345,@request,N'PAY1',291,'KWD';
IF (SELECT COUNT(*) FROM dbo.Payments WHERE OrderId=9 AND AmountPaid=291 AND PaymentStatus=1)<>1
   OR NOT EXISTS(SELECT 1 FROM dbo.Orders WHERE OrderId=9 AND PaymentStatus=1)
   OR EXISTS(SELECT 1 FROM dbo.InventoryReservations WHERE OrderId=9 AND Status<>'Consumed')
   OR (SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE SourceType='Consume' AND SourceId=9)<>2
    THROW 51824,'Paid inventory/ledger or retry incorrect.',1;
UPDATE dbo.Payments SET AmountPaid=290 WHERE OrderId=9;
BEGIN TRY
    EXEC dbo.ConfirmMarketplacePayment 9,12345,@request,N'PAY1',291,'KWD';
    THROW 51829,'Inconsistent paid ledger accepted on replay.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51805 THROW;
END CATCH;
UPDATE dbo.Payments SET AmountPaid=291 WHERE OrderId=9;
EXEC dbo.ConfirmMarketplacePayment 9,12345,@request,N'PAY1',291,'KWD';
PRINT 'Marketplace payment posting and paid replay consistency checks passed.';
GO

-- Exercise expiry in a rollback-only fixture; later lifecycle tests keep their existing data.
DECLARE @listing int=(SELECT l.VendorListingId FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId WHERE v.VendorCode=N'A');
DECLARE @lines nvarchar(max)=CONCAT('[{"listingId":',@listing,',"quantity":1}]');
DECLARE @until datetime2(3)=DATEADD(minute,15,SYSUTCDATETIME());
DECLARE @ref90 uniqueidentifier=NEWID(), @ref91 uniqueidentifier=NEWID(), @ref92 uniqueidentifier=NEWID(), @ref93 uniqueidentifier=NEWID();
DECLARE @available bigint=(SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE VendorListingId=@listing);
BEGIN TRANSACTION;
INSERT dbo.Orders(OrderId,CustomerId,MarketplaceRequestId,TotalAmount) VALUES
    (90,1,@ref90,101),(91,1,@ref91,101),(92,1,@ref92,101),(93,1,@ref93,101);
EXEC dbo.ReserveOrderInventory 90,@lines,@until;
EXEC dbo.ReserveOrderInventory 91,@lines,@until;
EXEC dbo.ReserveOrderInventory 92,@lines,@until;
EXEC dbo.ReserveOrderInventory 93,@lines,@until;
EXEC dbo.BeginMarketplacePayment 91,1;
EXEC dbo.BeginMarketplacePayment 92,1;
EXEC dbo.LinkMarketplaceInvoice 92,98765,@ref92;
UPDATE dbo.InventoryReservations SET CreatedAtUtc=DATEADD(minute,-2,SYSUTCDATETIME()),
    ExpiresAtUtc=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE OrderId IN (90,91,92);
IF (SELECT COUNT(*) FROM dbo.MarketplaceLinkedPaymentCandidates WHERE OrderId IN (9,90,91,92,93))<>1
   OR NOT EXISTS(SELECT 1 FROM dbo.MarketplaceLinkedPaymentCandidates WHERE OrderId=92 AND MerchantReference=@ref92 AND ExpectedAmount=101)
    THROW 51826,'Invalid automatic reconciliation candidates.',1;
UPDATE dbo.Orders SET TotalAmount=102 WHERE OrderId=92;
IF EXISTS(SELECT 1 FROM dbo.MarketplaceLinkedPaymentCandidates WHERE OrderId=92)
    THROW 51827,'Amount mismatch selected for reconciliation.',1;
UPDATE dbo.Orders SET TotalAmount=101 WHERE OrderId=92;
EXEC dbo.ReleaseExpiredUnstartedMarketplaceOrder 90;
EXEC dbo.ReleaseExpiredUnstartedMarketplaceOrder 90;
EXEC dbo.ReleaseExpiredUnstartedMarketplaceOrder 91;
EXEC dbo.ReleaseExpiredUnstartedMarketplaceOrder 92;
EXEC dbo.ReleaseExpiredUnstartedMarketplaceOrder 93;
EXEC dbo.ReleaseExpiredUnstartedMarketplaceOrder 9;
IF NOT EXISTS(SELECT 1 FROM dbo.InventoryReservations WHERE OrderId=90 AND Status='Released')
   OR (SELECT COUNT(*) FROM dbo.InventoryReservations WHERE OrderId IN (91,92,93) AND Status='Reserved')<>3
   OR (SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE SourceType='Release' AND SourceId=90)<>1
   OR (SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE VendorListingId=@listing)<>@available-3
   OR EXISTS(SELECT 1 FROM dbo.Payments WHERE OrderId IN (90,91,92,93))
    THROW 51825,'Unsafe expiry release or duplicate movement.',1;
ROLLBACK TRANSACTION;
PRINT 'Marketplace expiry protects starting, linked, paid and unexpired orders.';
GO

-- An unexpected payment record must block payment initiation and cancellation.
DECLARE @listing int=(SELECT l.VendorListingId FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId WHERE v.VendorCode=N'A');
DECLARE @lines nvarchar(max)=CONCAT('[{"listingId":',@listing,',"quantity":1}]');
DECLARE @until datetime2(3)=DATEADD(minute,15,SYSUTCDATETIME());
DECLARE @reference uniqueidentifier=NEWID();
INSERT dbo.Orders(OrderId,CustomerId,MarketplaceRequestId,TotalAmount) VALUES(94,1,@reference,101);
EXEC dbo.ReserveOrderInventory 94,@lines,@until;
INSERT dbo.Payments(OrderId,AmountPaid,PaymentMethod,PaymentStatus,TransactionId,PaidAt) VALUES(94,101,'Online',1,N'Unexpected',GETDATE());
BEGIN TRY
    EXEC dbo.BeginMarketplacePayment 94,1;
    THROW 51830,'Existing payment allowed duplicate invoice.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51802 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.CancelUnstartedMarketplaceOrder 94,1;
    THROW 51831,'Existing payment allowed stock release.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51806 THROW;
END CATCH;
IF EXISTS(SELECT 1 FROM dbo.MarketplacePaymentAttempts WHERE OrderId=94)
   OR NOT EXISTS(SELECT 1 FROM dbo.InventoryReservations WHERE OrderId=94 AND Status='Reserved')
    THROW 51832,'Unexpected payment changed order or stock.',1;
DELETE dbo.Payments WHERE OrderId=94;
EXEC dbo.CancelUnstartedMarketplaceOrder 94,1;
DELETE dbo.InventoryTransactions WHERE SourceId=94 AND SourceType IN ('Reserve','Release');
DELETE dbo.InventoryReservations WHERE OrderId=94;
DELETE dbo.Orders WHERE OrderId=94;
PRINT 'Existing payment prevents duplicate initiation and unsafe cancellation.';
GO
