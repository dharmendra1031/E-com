:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF DB_NAME() <> N'MarketplaceMigrationTest' THROW 51500, 'Disposable database required.', 1;
GO
:r marketplace-offers.sql
GO
DECLARE @b int = (SELECT VendorId FROM dbo.Vendors WHERE VendorCode = N'B');
DECLARE @lb int = (SELECT VendorListingId FROM dbo.VendorListings WHERE VendorId = @b);
IF (SELECT COUNT(*) FROM dbo.MarketplaceAvailableOffers WHERE ProductId = 1) <> 2
    THROW 51501, 'Expected two available approved sellers.', 1;
IF (SELECT MIN(Price) FROM dbo.MarketplaceAvailableOffers WHERE ProductId = 1) <> 95.000
    THROW 51502, 'Lowest price incorrect.', 1;
BEGIN TRANSACTION;
UPDATE dbo.Vendors SET Status = 'Suspended' WHERE VendorId = @b;
IF EXISTS (SELECT 1 FROM dbo.MarketplaceAvailableOffers WHERE ListingId = @lb)
    THROW 51503, 'Suspended vendor visible.', 1;
UPDATE dbo.Vendors SET Status = 'Approved' WHERE VendorId = @b;
UPDATE dbo.VendorListings SET Status = 'Pending' WHERE VendorListingId = @lb;
IF EXISTS (SELECT 1 FROM dbo.MarketplaceAvailableOffers WHERE ListingId = @lb)
    THROW 51504, 'Pending offer visible.', 1;
UPDATE dbo.VendorListings SET Status = 'Approved' WHERE VendorListingId = @lb;
UPDATE dbo.InventoryBalances SET ReservedQuantity = OnHandQuantity WHERE VendorListingId = @lb;
IF EXISTS (SELECT 1 FROM dbo.MarketplaceAvailableOffers WHERE ListingId = @lb)
    THROW 51505, 'Unavailable offer visible.', 1;
UPDATE dbo.Products SET IsActive = 0 WHERE ProductId = 1;
IF EXISTS (SELECT 1 FROM dbo.MarketplaceAvailableOffers WHERE ProductId = 1)
    THROW 51506, 'Inactive product visible.', 1;
ROLLBACK TRANSACTION;
PRINT 'Marketplace public offer filters passed.';
GO
