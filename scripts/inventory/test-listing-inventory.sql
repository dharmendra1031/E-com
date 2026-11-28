-- Run after marketplace fixture in the same disposable database.
:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF DB_NAME() <> N'MarketplaceMigrationTest' THROW 51320, 'Disposable marketplace DB required.', 1;
GO
ALTER TABLE dbo.Users ADD RoleId int NULL, IsActive bit NOT NULL DEFAULT (1);
CREATE TABLE dbo.Orders (OrderId int NOT NULL PRIMARY KEY);
ALTER TABLE dbo.Products ADD IsActive bit NOT NULL DEFAULT (1);
GO
INSERT dbo.Orders VALUES (1);
UPDATE dbo.Users SET RoleId = (SELECT RoleId FROM dbo.[Role] WHERE RoleName = N'VendorOwner') WHERE UserId = 1;
INSERT dbo.Users (UserId, RoleId) SELECT 2, RoleId FROM dbo.[Role] WHERE RoleName = N'VendorStaff';
UPDATE dbo.Vendors SET Status = 'Approved';
GO
:r inventory/02-create-schema.sql
GO
:r inventory/03-listing-scope.sql
GO
:r inventory/03-listing-scope.sql
GO
:r inventory/04-post-adjustment.sql
GO
SET XACT_ABORT OFF;
DECLARE @a int = (SELECT VendorId FROM dbo.Vendors WHERE VendorCode = N'A'),
        @b int = (SELECT VendorId FROM dbo.Vendors WHERE VendorCode = N'B');
DECLARE @la int = (SELECT VendorListingId FROM dbo.VendorListings WHERE VendorId = @a),
        @lb int = (SELECT VendorListingId FROM dbo.VendorListings WHERE VendorId = @b);
IF EXISTS (SELECT 1 FROM dbo.InventoryBalances) THROW 51321, 'Unexpected opening stock.', 1;
DECLARE @request uniqueidentifier = NEWID(), @other uniqueidentifier = NEWID();
UPDATE rp SET UpdatePage=0 FROM dbo.RolePermission rp JOIN dbo.PageMaster p ON p.PageId=rp.PageId WHERE p.PageCode='vendor.stock';
BEGIN TRY
    EXEC dbo.PostInventoryAdjustment @b, @lb, 1, @request, 10, N'Physical receipt';
    THROW 52122,'Revoked stock permission accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>51312 THROW;
END CATCH;
UPDATE rp SET UpdatePage=1 FROM dbo.RolePermission rp JOIN dbo.PageMaster p ON p.PageId=rp.PageId WHERE p.PageCode='vendor.stock';
EXEC dbo.PostInventoryAdjustment @b, @lb, 1, @request, 10, N'Physical receipt';
EXEC dbo.PostInventoryAdjustment @b, @lb, 1, @request, 10, N'Physical receipt';
IF (SELECT OnHandQuantity FROM dbo.InventoryBalances WHERE VendorListingId = @lb) <> 10
   OR (SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE RequestId = @request) <> 1
    THROW 51322, 'Duplicate posting.', 1;
BEGIN TRY
    EXEC dbo.PostInventoryAdjustment @b, @lb, 1, @request, 11, N'Physical receipt';
    THROW 51323, 'Changed retry accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51313 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.PostInventoryAdjustment @b, @la, 1, @other, 1, N'Wrong vendor';
    THROW 51324, 'Cross-vendor listing accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51312 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.PostInventoryAdjustment @a, @la, 1, @other, 1, N'Revoked member';
    THROW 51325, 'Revoked membership accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51312 THROW;
END CATCH;
INSERT dbo.VendorUsers (VendorId, UserId) VALUES (@b, 2);
BEGIN TRY
    EXEC dbo.PostInventoryAdjustment @b, @lb, 2, @other, 1, N'Staff write';
    THROW 51326, 'Staff write accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51312 THROW;
END CATCH;
BEGIN TRANSACTION;
UPDATE dbo.InventoryBalances SET ReservedQuantity = ReservedQuantity + 7
WHERE VendorListingId = @lb AND AvailableQuantity >= 7;
IF @@ROWCOUNT <> 1 THROW 51327, 'Reservation rejected.', 1;
INSERT dbo.InventoryReservations (OrderId, VendorListingId, Quantity, Status, ExpiresAtUtc)
VALUES (1, @lb, 7, 'Reserved', DATEADD(minute, 15, SYSUTCDATETIME()));
INSERT dbo.InventoryTransactions (VendorListingId, OnHandDelta, ReservedDelta, SourceType, SourceId, SourceLineId)
VALUES (@lb, 0, 7, 'Reserve', 1, 1);
COMMIT TRANSACTION;
BEGIN TRY
    EXEC dbo.PostInventoryAdjustment @b, @lb, 1, @other, -4, N'Excess adjustment';
    THROW 51328, 'Reserved stock reduced.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51314 THROW;
END CATCH;
EXEC dbo.PostInventoryAdjustment @b, @lb, 1, @other, -3, N'Count correction';
IF (SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE VendorListingId = @lb) <> 0
    OR EXISTS (SELECT 1 FROM dbo.InventoryBalances WHERE VendorListingId = @la)
    THROW 51329, 'Incorrect or cross-vendor balance.', 1;
PRINT 'Listing inventory procedure and constraints passed.';
GO
