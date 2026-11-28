:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 52520,'Disposable database required.',1;
GO
:r 12-refund-review.sql
GO
IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceRefundReview WHERE OrderId=9 AND ReturnedMerchandiseValue=101.000 AND InvoiceId=12345 AND PaymentId=N'PAY1')
    THROW 52521,'Original paid receipt evidence missing.',1;
IF (SELECT COUNT(*) FROM dbo.MarketplaceRefundReview)<>1 OR EXISTS(SELECT 1 FROM dbo.MarketplaceRefundReview WHERE OrderId=11)
    THROW 52522,'Unlinked return or unreturned order exposed.',1;
PRINT 'Refund evidence review checks passed.';
GO

-- A receipt with missing or incorrect inventory posting cannot be refund evidence.
BEGIN TRANSACTION;
DECLARE @receipt bigint=(SELECT ReturnReceiptId FROM dbo.MarketplaceRefundReview WHERE OrderId=9);
UPDATE dbo.InventoryTransactions SET OnHandDelta=OnHandDelta+1 WHERE SourceType='ReturnReceipt' AND SourceId=@receipt;
IF EXISTS(SELECT 1 FROM dbo.MarketplaceRefundReview WHERE ReturnReceiptId=@receipt)
BEGIN
    ROLLBACK;
    THROW 52523,'Inconsistent stock posting exposed as refund evidence.',1;
END;
ROLLBACK;
PRINT 'Refund evidence inventory posting validation passed.';
GO

:r 15-refund-attempts.sql
GO
:r 15-refund-attempts.sql
GO
DECLARE @ret int=(SELECT ReturnId FROM dbo.MarketplaceRefundReview WHERE OrderId=9),@ref uniqueidentifier=NEWID();
BEGIN TRY
 EXEC dbo.BeginMarketplaceRefund @ret,2,@ref,10;
 THROW 52620,'Unauthorized refund accepted.',1;
END TRY
BEGIN CATCH
 IF ERROR_NUMBER()<>52601 THROW;
END CATCH;
BEGIN TRY
 EXEC dbo.BeginMarketplaceRefund @ret,3,@ref,102;
 THROW 52621,'Excess refund accepted.',1;
END TRY
BEGIN CATCH
 IF ERROR_NUMBER()<>52602 THROW;
END CATCH;
EXEC dbo.BeginMarketplaceRefund @ret,3,@ref,10;
BEGIN TRY
 EXEC dbo.BeginMarketplaceRefund @ret,3,@ref,10;
 THROW 52622,'Duplicate refund accepted.',1;
END TRY
BEGIN CATCH
 IF ERROR_NUMBER()<>52603 THROW;
END CATCH;
BEGIN TRY
 EXEC dbo.ConfirmMarketplaceRefund @ret,3,@ref,12345,11,900001;
 THROW 52623,'Wrong proof accepted.',1;
END TRY
BEGIN CATCH
 IF ERROR_NUMBER()<>52605 THROW;
END CATCH;
EXEC dbo.ConfirmMarketplaceRefund @ret,3,@ref,12345,10,900001;
EXEC dbo.ConfirmMarketplaceRefund @ret,3,@ref,12345,10,900001;
IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceRefundAttempts WHERE ReturnId=@ret AND Amount=10 AND GatewayRefundId=900001 AND VerifiedBy=3)
 OR (SELECT COUNT(*) FROM dbo.Payments WHERE OrderId=9)<>1
 THROW 52624,'Refund evidence or original payment invalid.',1;
PRINT 'Refund attempt authorization, cap, duplicate and verified proof checks passed.';
GO
