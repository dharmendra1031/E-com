:ON ERROR EXIT
IF DB_NAME() <> N'MarketplaceMigrationTest' THROW 51610, 'Disposable database required.', 1;
GO
CREATE TABLE dbo.CartItems (CartItemId int IDENTITY PRIMARY KEY, UserId int NULL, ProductId int NOT NULL, Quantity int NOT NULL, Price decimal(18,3) NOT NULL);
INSERT dbo.CartItems(UserId,ProductId,Quantity,Price) VALUES (0,1,1,100),(1,1,2,100);
GO
:r 03-cart-listing.sql
GO
:r 03-cart-listing.sql
GO
SET XACT_ABORT OFF;
DECLARE @a int = (SELECT l.VendorListingId FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId WHERE v.VendorCode=N'A'),
        @b int = (SELECT l.VendorListingId FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId WHERE v.VendorCode=N'B');
INSERT dbo.CartItems(UserId,ProductId,Quantity,Price,VendorListingId) VALUES (1,1,1,101,@a),(1,1,2,95,@b);
IF (SELECT COUNT(*) FROM dbo.CartItems WHERE UserId=1 AND VendorListingId IS NOT NULL) <> 2
    THROW 51611, 'Independent listing cart rows missing.', 1;
IF (SELECT COUNT(*) FROM dbo.CartItems WHERE VendorListingId IS NULL) <> 2
    THROW 51612, 'Legacy cart changed.', 1;
BEGIN TRY
    INSERT dbo.CartItems(UserId,ProductId,Quantity,Price,VendorListingId) VALUES (1,1,1,101,@a);
    THROW 51613, 'Duplicate seller line accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
BEGIN TRY
    INSERT dbo.CartItems(UserId,ProductId,Quantity,Price,VendorListingId) VALUES (0,1,1,101,@a);
    THROW 51614, 'Guest seller line accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 547 THROW;
END CATCH;
PRINT 'Marketplace cart schema checks passed.';
GO
