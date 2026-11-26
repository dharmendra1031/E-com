-- Run after test-listing-inventory.sql in the same disposable database.
:ON ERROR EXIT
IF DB_NAME() <> N'MarketplaceMigrationTest' THROW 51420, 'Disposable database required.', 1;
GO
:r inventory/05-order-reservations.sql
GO
SET NOCOUNT ON;
SET XACT_ABORT OFF;
DECLARE @a int = (SELECT VendorId FROM dbo.Vendors WHERE VendorCode = N'A'),
        @b int = (SELECT VendorId FROM dbo.Vendors WHERE VendorCode = N'B');
DECLARE @la int = (SELECT VendorListingId FROM dbo.VendorListings WHERE VendorId = @a),
        @lb int = (SELECT VendorListingId FROM dbo.VendorListings WHERE VendorId = @b);
UPDATE dbo.VendorListings SET Status = 'Approved';
INSERT dbo.Orders VALUES (2),(3),(4),(5),(6),(7),(8);
UPDATE dbo.VendorUsers SET IsActive = 1 WHERE VendorId = @a AND UserId = 1;
DECLARE @request uniqueidentifier = NEWID();
EXEC dbo.PostInventoryAdjustment @a, @la, 1, @request, 10, N'Test receipt A';
SET @request = NEWID();
EXEC dbo.PostInventoryAdjustment @b, @lb, 1, @request, 10, N'Test receipt B';
DECLARE @expiry datetime2(3) = DATEADD(minute,15,SYSUTCDATETIME());
DECLARE @multi nvarchar(max) = CONCAT('[{"listingId":',@la,',"quantity":4},{"listingId":',@lb,',"quantity":6}]');
DECLARE @bad nvarchar(max) = CONCAT('[{"listingId":',@la,',"quantity":2},{"listingId":',@lb,',"quantity":99}]');
DECLARE @single nvarchar(max) = CONCAT('[{"listingId":',@la,',"quantity":6}]');
EXEC dbo.ReserveOrderInventory 2,@multi,@expiry;
EXEC dbo.ReserveOrderInventory 2,@multi,@expiry;
IF (SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE SourceType = 'Reserve' AND SourceId = 2) <> 2
   OR (SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE VendorListingId = @la) <> 6
   OR (SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE VendorListingId = @lb) <> 4
    THROW 51421, 'Reservation balance or retry incorrect.', 1;
BEGIN TRY
    EXEC dbo.ReserveOrderInventory 2,@single,@expiry;
    THROW 51422, 'Changed retry accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51402 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.ReserveOrderInventory 3,@bad,@expiry;
    THROW 51423, 'Oversized basket accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51404 THROW;
END CATCH;
IF EXISTS (SELECT 1 FROM dbo.InventoryReservations WHERE OrderId = 3)
   OR EXISTS (SELECT 1 FROM dbo.InventoryTransactions WHERE SourceType = 'Reserve' AND SourceId = 3)
   OR (SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE VendorListingId = @la) <> 6
    THROW 51424, 'Partial basket reservation persisted.', 1;
BEGIN TRY
    EXEC dbo.FinalizeOrderInventory 2,'Released','VerifiedExpiry';
    THROW 51425, 'Premature expiry accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51413 THROW;
END CATCH;
EXEC dbo.FinalizeOrderInventory 2,'Consumed','PaymentConfirmed';
EXEC dbo.FinalizeOrderInventory 2,'Consumed','PaymentConfirmed';
IF (SELECT OnHandQuantity FROM dbo.InventoryBalances WHERE VendorListingId = @la) <> 6
   OR (SELECT OnHandQuantity FROM dbo.InventoryBalances WHERE VendorListingId = @lb) <> 11
   OR (SELECT ReservedQuantity FROM dbo.InventoryBalances WHERE VendorListingId = @lb) <> 7
   OR (SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE SourceType = 'Consume' AND SourceId = 2) <> 2
    THROW 51426, 'Consume incorrect or duplicated.', 1;
BEGIN TRY
    EXEC dbo.FinalizeOrderInventory 2,'Released','PaymentFailed';
    THROW 51427, 'Consumed stock released.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51412 THROW;
END CATCH;
EXEC dbo.ReserveOrderInventory 4,@single,@expiry;
BEGIN TRY
    EXEC dbo.ReserveOrderInventory 5,@single,@expiry;
    THROW 51428, 'Last units reserved twice.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51404 THROW;
END CATCH;
EXEC dbo.FinalizeOrderInventory 4,'Released','UnpaidCancellation';
EXEC dbo.FinalizeOrderInventory 4,'Released','UnpaidCancellation';
IF (SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE VendorListingId = @la) <> 6
    THROW 51429, 'Release incorrect.', 1;
BEGIN TRY
    EXEC dbo.FinalizeOrderInventory 4,'Consumed','PaymentConfirmed';
    THROW 51430, 'Late payment consumed released stock.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51412 THROW;
END CATCH;
EXEC dbo.ReserveOrderInventory 5,@single,@expiry;
EXEC dbo.FinalizeOrderInventory 5,'Released','PaymentFailed';
EXEC dbo.ReserveOrderInventory 6,@single,@expiry;
UPDATE dbo.InventoryReservations SET CreatedAtUtc = DATEADD(minute,-2,SYSUTCDATETIME()),
    ExpiresAtUtc = DATEADD(minute,-1,SYSUTCDATETIME()) WHERE OrderId = 6;
EXEC dbo.FinalizeOrderInventory 6,'Released','VerifiedExpiry';
IF (SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE VendorListingId = @la) <> 6
    THROW 51431, 'Verified expiry release incorrect.', 1;
UPDATE dbo.VendorListings SET Status = 'Inactive' WHERE VendorListingId = @la;
BEGIN TRY
    EXEC dbo.ReserveOrderInventory 7,@single,@expiry;
    THROW 51432, 'Inactive listing reserved.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51403 THROW;
END CATCH;
UPDATE dbo.VendorListings SET Status = 'Approved' WHERE VendorListingId = @la;
BEGIN TRANSACTION;
EXEC dbo.ReserveOrderInventory 8,@single,@expiry;
ROLLBACK TRANSACTION;
IF EXISTS (SELECT 1 FROM dbo.InventoryReservations WHERE OrderId = 8)
   OR (SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE VendorListingId = @la) <> 6
    THROW 51433, 'Outer order rollback did not undo reservation.', 1;
PRINT 'Order inventory lifecycle checks passed.';
GO
