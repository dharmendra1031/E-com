:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 52320,'Disposable database required.',1;
GO
:r 10-marketplace-returns.sql
GO
INSERT dbo.[Role](RoleName,RoleNameArb,IsActive,CreatedAt,UpdatedAt) VALUES('Admin',N'Admin',1,SYSUTCDATETIME(),SYSUTCDATETIME());
INSERT dbo.Users(UserId,RoleId) SELECT 3,RoleId FROM dbo.[Role] WHERE RoleName='Admin';
DECLARE @item int=(SELECT OrderItemId FROM dbo.OrderItems WHERE OrderId=9 AND Quantity=1);
DECLARE @listing int=(SELECT VendorListingId FROM dbo.OrderItems WHERE OrderItemId=@item),@request uniqueidentifier=NEWID(),@other uniqueidentifier=NEWID();
DECLARE @before bigint=(SELECT OnHandQuantity FROM dbo.InventoryBalances WHERE VendorListingId=@listing);
BEGIN TRY
    EXEC dbo.RequestMarketplaceReturn 2,@item,@request,1,N'Return';
    THROW 52321,'Cross customer return accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52301 THROW;
END CATCH;
EXEC dbo.RequestMarketplaceReturn 1,@item,@request,1,N'Return';
EXEC dbo.RequestMarketplaceReturn 1,@item,@request,1,N'Return';
DECLARE @id int=(SELECT ReturnId FROM dbo.MarketplaceReturnRequests WHERE RequestId=@request);
BEGIN TRY
    EXEC dbo.RequestMarketplaceReturn 1,@item,@other,1,N'Excess';
    THROW 52322,'Excess pending request accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52303 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.ReviewMarketplaceReturn @id,2,'Pending','Approved';
    THROW 52323,'Non-admin reviewed return.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52305 THROW;
END CATCH;
EXEC dbo.ReviewMarketplaceReturn @id,3,'Pending','Approved';
IF (SELECT OnHandQuantity FROM dbo.InventoryBalances WHERE VendorListingId=@listing)<>@before
    THROW 52324,'Approval restocked before receipt.',1;
EXEC dbo.ReviewMarketplaceReturn @id,3,'Approved','Received';
EXEC dbo.ReviewMarketplaceReturn @id,3,'Approved','Received';
IF (SELECT OnHandQuantity FROM dbo.InventoryBalances WHERE VendorListingId=@listing)<>@before+1
   OR (SELECT COUNT(*) FROM dbo.VendorReturnReceipts WHERE ReceiptRequestId=@request)<>1
   OR NOT EXISTS(SELECT 1 FROM dbo.MarketplaceReturnReview WHERE ReturnId=@id AND Status='Received')
   OR NOT EXISTS(SELECT 1 FROM dbo.MarketplaceReturnItems WHERE OrderItemId=@item AND RemainingQuantity=0)
   OR (SELECT COUNT(*) FROM dbo.Payments WHERE OrderId=9)<>1
    THROW 52325,'Receipt or refund isolation incorrect.',1;
PRINT 'Marketplace return request and approval checks passed.';
GO
