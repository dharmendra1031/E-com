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
