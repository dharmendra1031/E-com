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

:r 16-settlement-ledger.sql
GO
:r 16-settlement-ledger.sql
GO
DECLARE @vo int=(SELECT VendorOrderId FROM dbo.VendorOrders WHERE OrderId=11);
BEGIN TRY
    EXEC dbo.PostVendorSettlementPreview @vo,2;
    THROW 52720,'Non-admin posted ledger.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52701 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.PostVendorSettlementPreview @vo,3;
    THROW 52721,'Undelivered sale posted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52703 THROW;
END CATCH;
IF EXISTS(SELECT 1 FROM dbo.VendorSettlementLedger) THROW 52722,'Unexpected ledger entries.',1;
GO
-- Positive scenarios in a rollback-only fixture. Existing orders remain unchanged.
BEGIN TRANSACTION;
DECLARE @vo int=(SELECT VendorOrderId FROM dbo.VendorOrders WHERE OrderId=11),
        @vendor int=(SELECT VendorId FROM dbo.VendorOrders WHERE OrderId=11),
        @item int=(SELECT OrderItemId FROM dbo.OrderItems WHERE OrderId=11),
        @request uniqueidentifier=NEWID(),@refund uniqueidentifier=NEWID(),@returnId int;
EXEC dbo.AdvanceVendorFulfillment @vendor,@vo,3,'AwaitingPacking','Packed',1,0;
EXEC dbo.AdvanceVendorFulfillment @vendor,@vo,3,'Packed','HandedOver',1,0;
EXEC dbo.AdvanceVendorFulfillment @vendor,@vo,3,'HandedOver','Delivered',1,1;
EXEC dbo.PostVendorSettlementPreview @vo,3;
EXEC dbo.PostVendorSettlementPreview @vo,3;
IF (SELECT COUNT(*) FROM dbo.VendorSettlementLedger WHERE VendorOrderId=@vo)<>1
    OR NOT EXISTS(SELECT 1 FROM dbo.MarketplaceSettlementReview WHERE VendorOrderId=@vo AND LedgerNet=90.900 AND LedgerCurrent=1 AND SettlementEligible=1)
    THROW 52723,'Sale ledger or idempotency failed.',1;
EXEC dbo.RequestMarketplaceReturn 1,@item,@request,1,N'Settlement fixture';
SET @returnId=(SELECT ReturnId FROM dbo.MarketplaceReturnRequests WHERE RequestId=@request);
IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceSettlementReview WHERE VendorOrderId=@vo AND SettlementEligible=0)
    THROW 52724,'Open return did not hold settlement.',1;
EXEC dbo.ReviewMarketplaceReturn @returnId,3,'Pending','Approved';
EXEC dbo.ReviewMarketplaceReturn @returnId,3,'Approved','Received';
EXEC dbo.BeginMarketplaceRefund @returnId,3,@refund,20;
IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceSettlementReview WHERE VendorOrderId=@vo AND SettlementEligible=0 AND VerifiedRefundValue=0 AND LedgerNet=90.900)
    THROW 52725,'Unverified refund affected ledger.',1;
EXEC dbo.ConfirmMarketplaceRefund @returnId,3,@refund,12346,20,900002;
IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceSettlementReview WHERE VendorOrderId=@vo AND SettlementEligible=1 AND LedgerCurrent=0 AND VerifiedRefundValue=20)
    THROW 52726,'Verified refund not available for synchronization.',1;
EXEC dbo.PostVendorSettlementPreview @vo,3;
EXEC dbo.PostVendorSettlementPreview @vo,3;
IF (SELECT COUNT(*) FROM dbo.VendorSettlementLedger WHERE VendorOrderId=@vo)<>2
    OR NOT EXISTS(SELECT 1 FROM dbo.VendorSettlementLedger WHERE VendorOrderId=@vo AND ReturnId=@returnId AND MerchandiseDelta=-20 AND CommissionDelta=-2 AND PostedBy=3)
    OR NOT EXISTS(SELECT 1 FROM dbo.MarketplaceSettlementReview WHERE VendorOrderId=@vo AND LedgerNet=72.900 AND LedgerCommissionReversal=2 AND LedgerCurrent=1)
    OR NOT EXISTS(SELECT 1 FROM dbo.VendorOrders WHERE VendorOrderId=@vo AND CommissionAmount=10.100 AND CommissionRatePercent=10)
    OR EXISTS(SELECT 1 FROM dbo.VendorSettlementLedger WHERE VendorOrderId<>@vo)
    THROW 52727,'Refund reversal, snapshot or isolation failed.',1;
ROLLBACK;
PRINT 'Settlement preview sale, late return, verified refund, commission and duplicate checks passed.';
GO

-- A full verified refund reverses the complete original commission.
BEGIN TRANSACTION;
DECLARE @vo int=(SELECT VendorOrderId FROM dbo.VendorOrders WHERE OrderId=11),
        @vendor int=(SELECT VendorId FROM dbo.VendorOrders WHERE OrderId=11),
        @item int=(SELECT OrderItemId FROM dbo.OrderItems WHERE OrderId=11),
        @request uniqueidentifier=NEWID(),@refund uniqueidentifier=NEWID(),@returnId int;
EXEC dbo.AdvanceVendorFulfillment @vendor,@vo,3,'AwaitingPacking','Packed',1,0;
EXEC dbo.AdvanceVendorFulfillment @vendor,@vo,3,'Packed','HandedOver',1,0;
EXEC dbo.AdvanceVendorFulfillment @vendor,@vo,3,'HandedOver','Delivered',1,1;
EXEC dbo.RequestMarketplaceReturn 1,@item,@request,1,N'Full return fixture';
SET @returnId=(SELECT ReturnId FROM dbo.MarketplaceReturnRequests WHERE RequestId=@request);
EXEC dbo.ReviewMarketplaceReturn @returnId,3,'Pending','Approved';
EXEC dbo.ReviewMarketplaceReturn @returnId,3,'Approved','Received';
EXEC dbo.BeginMarketplaceRefund @returnId,3,@refund,101;
EXEC dbo.ConfirmMarketplaceRefund @returnId,3,@refund,12346,101,900003;
EXEC dbo.PostVendorSettlementPreview @vo,3;
IF NOT EXISTS(SELECT 1 FROM dbo.MarketplaceSettlementReview WHERE VendorOrderId=@vo AND LedgerNet=0 AND LedgerCommissionReversal=10.100 AND LedgerCurrent=1)
    THROW 52728,'Full refund did not reverse original commission.',1;
ROLLBACK;
PRINT 'Full refund commission reversal checks passed.';
GO
