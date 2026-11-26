:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 51920,'Disposable database required.',1;
GO
:r 07-return-receipts.sql
GO
DECLARE @item int=(SELECT TOP(1) OrderItemId FROM dbo.OrderItems WHERE OrderId=9 AND Quantity=2),
        @request uniqueidentifier=NEWID(),@other uniqueidentifier=NEWID();
DECLARE @listing int=(SELECT VendorListingId FROM dbo.OrderItems WHERE OrderItemId=@item);
DECLARE @before bigint=(SELECT OnHandQuantity FROM dbo.InventoryBalances WHERE VendorListingId=@listing);
EXEC dbo.PostMarketplaceReturnStock @item,@request,1,N'Accepted physical return';
EXEC dbo.PostMarketplaceReturnStock @item,@request,1,N'Accepted physical return';
IF (SELECT OnHandQuantity FROM dbo.InventoryBalances WHERE VendorListingId=@listing)<>@before+1
    THROW 51921,'Return retry duplicated stock.',1;
BEGIN TRY
    EXEC dbo.PostMarketplaceReturnStock @item,@request,2,N'Changed return';
    THROW 51922,'Changed return retry accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51902 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.PostMarketplaceReturnStock @item,@other,2,N'Excess return';
    THROW 51923,'Excess return accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51903 THROW;
END CATCH;
EXEC dbo.PostMarketplaceReturnStock @item,@other,1,N'Second accepted return';
IF (SELECT OnHandQuantity FROM dbo.InventoryBalances WHERE VendorListingId=@listing)<>@before+2
   OR (SELECT COUNT(*) FROM dbo.VendorReturnReceipts WHERE OrderItemId=@item)<>2
   OR (SELECT COUNT(*) FROM dbo.Payments WHERE OrderId=9)<>1
    THROW 51924,'Return stock or original payment incorrect.',1;
PRINT 'Marketplace physical return stock checks passed.';
GO
