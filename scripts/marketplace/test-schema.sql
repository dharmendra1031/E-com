-- Disposable database only. Run with sqlcmd -b from scripts/marketplace.
:ON ERROR EXIT
IF DB_NAME() <> N'MarketplaceMigrationTest'
    THROW 51109, 'Use MarketplaceMigrationTest only.', 1;
GO
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.Products', N'U') IS NOT NULL OR OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
    THROW 51110, 'Use a new disposable database.', 1;
CREATE TABLE dbo.Products (ProductId int NOT NULL PRIMARY KEY);
CREATE TABLE dbo.Users (UserId int NOT NULL PRIMARY KEY);
CREATE TABLE dbo.[Role] (RoleId int IDENTITY PRIMARY KEY, RoleName nvarchar(100) NOT NULL, RoleNameArb nvarchar(100) NOT NULL, IsActive bit NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NOT NULL);
INSERT dbo.Products VALUES (1), (2);
INSERT dbo.Users VALUES (1);
GO
:r 01-create-schema.sql
GO
:r 01-create-schema.sql
GO
:r 02-seed-vendor-roles.sql
GO
:r 02-seed-vendor-roles.sql
GO
IF (SELECT COUNT(*) FROM dbo.[Role]) <> 3
    THROW 51122, 'Role seed did not create exactly three roles.', 1;
IF EXISTS (SELECT 1 FROM dbo.Vendors) OR EXISTS (SELECT 1 FROM dbo.VendorListings) OR EXISTS (SELECT 1 FROM dbo.VendorUsers)
    THROW 51111, 'Migration unexpectedly seeded data.', 1;

INSERT dbo.Vendors (VendorCode, Name) VALUES (N'A', N'Seller A'), (N'B', N'Seller B');
SET XACT_ABORT OFF;
DECLARE @A int = (SELECT VendorId FROM dbo.Vendors WHERE VendorCode = N'A');
DECLARE @B int = (SELECT VendorId FROM dbo.Vendors WHERE VendorCode = N'B');
INSERT dbo.VendorUsers (VendorId, UserId) VALUES (@A, 1);
INSERT dbo.VendorUsers (VendorId, UserId) VALUES (@B, 1);
BEGIN TRY
    INSERT dbo.VendorUsers (VendorId, UserId) VALUES (@A, 1);
    THROW 51123, 'Duplicate membership accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
END CATCH;
UPDATE dbo.VendorUsers SET IsActive = 0 WHERE VendorId = @A AND UserId = 1;
IF NOT EXISTS (SELECT 1 FROM dbo.VendorUsers WHERE VendorId = @B AND UserId = 1 AND IsActive = 1)
    THROW 51124, 'Revoking membership affected another vendor.', 1;
INSERT dbo.VendorListings (VendorId, ProductId, SellerSku, Price)
VALUES (@A, 1, N'SKU1', 100.000), (@B, 1, N'SKU1', 95.000);
IF (SELECT COUNT(*) FROM dbo.VendorListings WHERE ProductId = 1 AND Status = 'Pending') <> 2
    THROW 51112, 'Expected two pending offers for one catalog product.', 1;

BEGIN TRY
    INSERT dbo.VendorListings (VendorId, ProductId, SellerSku, Price) VALUES (@A, 1, N'SKU2', 90);
    THROW 51113, 'Duplicate seller/product was accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
END CATCH;
BEGIN TRY
    INSERT dbo.VendorListings (VendorId, ProductId, SellerSku, Price) VALUES (@A, 2, N'SKU1', 90);
    THROW 51114, 'Duplicate seller SKU was accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
END CATCH;
BEGIN TRY
    INSERT dbo.VendorListings (VendorId, ProductId, SellerSku, Price) VALUES (@A, 2, N'SKU2', -1);
    THROW 51115, 'Negative price was accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 547 THROW;
END CATCH;
BEGIN TRY
    INSERT dbo.VendorUsers (VendorId, UserId) VALUES (@B, 999);
    THROW 51116, 'Orphan membership was accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 547 THROW;
END CATCH;
BEGIN TRY
    INSERT dbo.VendorListings (VendorId, ProductId, SellerSku, Price) VALUES (@A, 999, N'SKU3', 10);
    THROW 51117, 'Orphan product was accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 547 THROW;
END CATCH;
DECLARE @oldVersion binary(8) = (SELECT RowVersion FROM dbo.VendorListings WHERE VendorId = @A AND ProductId = 1);
UPDATE dbo.VendorListings SET Price = 101.000 WHERE VendorId = @A AND ProductId = 1 AND RowVersion = @oldVersion;
IF @@ROWCOUNT <> 1 THROW 51125, 'Expected listing update rejected.', 1;
UPDATE dbo.VendorListings SET Price = 102.000 WHERE VendorId = @A AND ProductId = 1 AND RowVersion = @oldVersion;
IF @@ROWCOUNT <> 0 THROW 51126, 'Stale listing version accepted.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.VendorListings WHERE VendorId = @B AND ProductId = 1 AND Price = 95.000)
    THROW 51127, 'Updating one vendor changed another price.', 1;
PRINT 'Marketplace schema fixture passed.';
