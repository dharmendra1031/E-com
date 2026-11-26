-- Disposable database only. Run with sqlcmd -b from scripts/marketplace.
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.Products', N'U') IS NOT NULL OR OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
    THROW 51110, 'Use a new disposable database.', 1;
CREATE TABLE dbo.Products (ProductId int NOT NULL PRIMARY KEY);
CREATE TABLE dbo.Users (UserId int NOT NULL PRIMARY KEY);
INSERT dbo.Products VALUES (1), (2);
INSERT dbo.Users VALUES (1);
GO
:r 01-create-schema.sql
GO
:r 01-create-schema.sql
GO
IF EXISTS (SELECT 1 FROM dbo.Vendors) OR EXISTS (SELECT 1 FROM dbo.VendorListings) OR EXISTS (SELECT 1 FROM dbo.VendorUsers)
    THROW 51111, 'Migration unexpectedly seeded data.', 1;

INSERT dbo.Vendors (VendorCode, Name) VALUES (N'A', N'Seller A'), (N'B', N'Seller B');
DECLARE @A int = (SELECT VendorId FROM dbo.Vendors WHERE VendorCode = N'A');
DECLARE @B int = (SELECT VendorId FROM dbo.Vendors WHERE VendorCode = N'B');
INSERT dbo.VendorUsers (VendorId, UserId) VALUES (@A, 1);
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
PRINT 'Marketplace schema fixture passed.';
