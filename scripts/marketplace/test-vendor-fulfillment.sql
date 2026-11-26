:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 52020,'Disposable database required.',1;
GO
:r 08-vendor-fulfillment.sql
GO
:r 08-vendor-fulfillment.sql
GO
DECLARE @a int=(SELECT VendorId FROM dbo.Vendors WHERE VendorCode='A'),
        @b int=(SELECT VendorId FROM dbo.Vendors WHERE VendorCode='B');
DECLARE @vo int=(SELECT VendorOrderId FROM dbo.VendorOrders WHERE OrderId=9 AND VendorId=@b);
BEGIN TRY
    EXEC dbo.AdvanceVendorFulfillment @b,@vo,2,'AwaitingPacking','Packed';
    THROW 52021,'Staff updated fulfillment.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52001 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.AdvanceVendorFulfillment @a,@vo,1,'AwaitingPacking','Packed';
    THROW 52022,'Cross vendor update accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52002 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.AdvanceVendorFulfillment @b,@vo,1,'AwaitingPacking','HandedOver';
    THROW 52023,'Skipped packing accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52000 THROW;
END CATCH;
EXEC dbo.AdvanceVendorFulfillment @b,@vo,1,'AwaitingPacking','Packed';
EXEC dbo.AdvanceVendorFulfillment @b,@vo,1,'AwaitingPacking','Packed';
EXEC dbo.AdvanceVendorFulfillment @b,@vo,1,'Packed','HandedOver';
IF (SELECT COUNT(*) FROM dbo.VendorOrderFulfillmentHistory WHERE VendorOrderId=@vo)<>2
   OR NOT EXISTS(SELECT 1 FROM dbo.VendorOrders WHERE VendorOrderId=@vo AND FulfillmentStatus='HandedOver')
   OR NOT EXISTS(SELECT 1 FROM dbo.VendorOrders WHERE OrderId=9 AND VendorId=@a AND FulfillmentStatus='AwaitingPacking')
    THROW 52024,'Fulfillment audit or isolation incorrect.',1;
PRINT 'Marketplace vendor fulfillment checks passed.';
GO
