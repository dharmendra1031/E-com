:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 52420,'Disposable database required.',1;
GO
:r 11-vendor-finance.sql
GO
:r 11-vendor-finance.sql
GO
DECLARE @a int=(SELECT VendorId FROM dbo.Vendors WHERE VendorCode='A'),@b int=(SELECT VendorId FROM dbo.Vendors WHERE VendorCode='B');
DECLARE @listing int=(SELECT VendorListingId FROM dbo.VendorListings WHERE VendorId=@a),@req uniqueidentifier=NEWID();
DECLARE @expiry datetime2(3)=DATEADD(minute,15,SYSUTCDATETIME());
DECLARE @quote nvarchar(max)=CONCAT('[{"listingId":',@listing,',"quantity":1,"price":101.000}]');
BEGIN TRY
    EXEC dbo.SetVendorCommission @a,2,0,10;
    THROW 52421,'Non-admin changed rate.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52401 THROW;
END CATCH;
INSERT dbo.CartItems(UserId,ProductId,Quantity,Price,VendorListingId) VALUES(1,1,1,101,@listing);
BEGIN TRY
    BEGIN TRANSACTION;
    INSERT dbo.Orders(OrderId,CustomerId,MarketplaceRequestId,TotalAmount) VALUES(11,1,@req,101);
    EXEC dbo.AttachMarketplaceOrder 11,1,@quote,@expiry;
    EXEC dbo.SnapshotVendorCommission 11;
    THROW 52422,'Missing policy accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52406 THROW;
END CATCH;
IF EXISTS(SELECT 1 FROM dbo.Orders WHERE OrderId=11) OR NOT EXISTS(SELECT 1 FROM dbo.CartItems WHERE UserId=1 AND VendorListingId=@listing)
    THROW 52423,'Missing policy rollback incomplete.',1;
EXEC dbo.SetVendorCommission @a,3,0,10;
DECLARE @policy int=(SELECT MAX(PolicyId) FROM dbo.VendorCommissionPolicies WHERE VendorId=@a);
BEGIN TRANSACTION;
INSERT dbo.Orders(OrderId,CustomerId,MarketplaceRequestId,TotalAmount) VALUES(11,1,@req,101);
EXEC dbo.AttachMarketplaceOrder 11,1,@quote,@expiry;
EXEC dbo.SnapshotVendorCommission 11;
COMMIT;
EXEC dbo.SetVendorCommission @a,3,@policy,15;
IF NOT EXISTS(SELECT 1 FROM dbo.VendorOrders WHERE OrderId=11 AND CommissionPolicyId=@policy AND CommissionRatePercent=10 AND CommissionAmount=10.100)
    THROW 52424,'Historical commission changed.',1;
BEGIN TRY
    EXEC dbo.SetVendorCommission @a,3,@policy,20;
    THROW 52425,'Stale rate change accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52403 THROW;
END CATCH;
EXEC dbo.BeginMarketplacePayment 11,1;
EXEC dbo.LinkMarketplaceInvoice 11,12346,@req;
EXEC dbo.ConfirmMarketplacePayment 11,12346,@req,N'PAY2',101,'KWD';
IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceFinanceReview WHERE OrderId=11 AND MerchandiseAfterCommission=90.900 AND ReviewState='Financial review required')
   OR EXISTS(SELECT 1 FROM dbo.MarketplaceFinanceReview WHERE OrderId=9 AND CommissionAmount IS NOT NULL)
    THROW 52426,'Finance projection incorrect or retroactive.',1;
PRINT 'Marketplace commission version, snapshot and finance review checks passed.';
GO
