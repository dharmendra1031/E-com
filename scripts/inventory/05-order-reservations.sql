SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
-- Internal order service only. No public endpoints. Caller must authenticate and validate order/payment.
CREATE OR ALTER PROCEDURE dbo.ReserveOrderInventory
    @OrderId int,
    @Lines nvarchar(max),
    @ExpiresAtUtc datetime2(3)
AS
BEGIN
    SET ANSI_PADDING ON;
    SET ANSI_WARNINGS ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @OrderId IS NULL OR @OrderId <= 0 OR @ExpiresAtUtc IS NULL OR @Lines IS NULL
        OR DATALENGTH(@Lines) > 100000 OR ISJSON(@Lines) <> 1 OR LEFT(LTRIM(@Lines),1) <> '['
        THROW 51400, 'Invalid reservation request.', 1;

    DECLARE @items TABLE (VendorListingId int NOT NULL PRIMARY KEY, Quantity bigint NOT NULL);
    IF EXISTS (SELECT 1 FROM OPENJSON(@Lines) WHERE [type] <> 5)
        THROW 51400, 'Invalid reservation item.', 1;
    DECLARE @raw TABLE (VendorListingId int NULL, Quantity bigint NULL);
    INSERT @raw SELECT VendorListingId, Quantity FROM OPENJSON(@Lines)
        WITH (VendorListingId int '$.listingId', Quantity bigint '$.quantity');
    IF NOT EXISTS (SELECT 1 FROM @raw) OR (SELECT COUNT(*) FROM @raw) > 100
       OR EXISTS (SELECT 1 FROM @raw WHERE VendorListingId IS NULL OR VendorListingId <= 0
                  OR Quantity IS NULL OR Quantity <= 0 OR Quantity > 1000000000)
       OR EXISTS (SELECT 1 FROM @raw GROUP BY VendorListingId HAVING COUNT(*) > 1)
        THROW 51400, 'Invalid or duplicate reservation items.', 1;
    INSERT @items SELECT VendorListingId, Quantity FROM @raw;

    BEGIN TRY
        BEGIN TRANSACTION;
        -- All order inventory procedures acquire this order lock first.
        IF NOT EXISTS (SELECT 1 FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE OrderId = @OrderId)
            THROW 51401, 'Order not found.', 1;

        IF EXISTS (SELECT 1 FROM dbo.InventoryReservations WHERE OrderId = @OrderId)
        BEGIN
            IF EXISTS (SELECT VendorListingId, Quantity FROM @items
                       EXCEPT SELECT VendorListingId, Quantity FROM dbo.InventoryReservations WHERE OrderId = @OrderId)
               OR EXISTS (SELECT VendorListingId, Quantity FROM dbo.InventoryReservations WHERE OrderId = @OrderId
                          EXCEPT SELECT VendorListingId, Quantity FROM @items)
               OR EXISTS (SELECT 1 FROM dbo.InventoryReservations WHERE OrderId = @OrderId
                          AND (Status <> 'Reserved' OR ExpiresAtUtc <> @ExpiresAtUtc))
                THROW 51402, 'Reservation request changed or already finalized.', 1;
            COMMIT TRANSACTION;
            RETURN;
        END;

        IF @ExpiresAtUtc <= SYSUTCDATETIME() OR @ExpiresAtUtc > DATEADD(hour,24,SYSUTCDATETIME())
            THROW 51400, 'Invalid reservation expiry.', 1;
        -- Stable listing lock order for multi-item transactions.
        DECLARE @id int = 0, @qty bigint;
        WHILE 1 = 1
        BEGIN
            SELECT @id = MIN(VendorListingId) FROM @items WHERE VendorListingId > @id;
            IF @id IS NULL BREAK;
            SELECT @qty = Quantity FROM @items WHERE VendorListingId = @id;
            IF NOT EXISTS (
                SELECT 1 FROM dbo.VendorListings l WITH (UPDLOCK,HOLDLOCK)
                JOIN dbo.Vendors v WITH (HOLDLOCK) ON v.VendorId = l.VendorId
                JOIN dbo.Products p WITH (HOLDLOCK) ON p.ProductId = l.ProductId
                WHERE l.VendorListingId = @id AND l.Status = 'Approved' AND v.Status = 'Approved' AND p.IsActive = 1)
                THROW 51403, 'Listing not available.', 1;
            UPDATE dbo.InventoryBalances SET ReservedQuantity = ReservedQuantity + @qty, UpdatedAtUtc = SYSUTCDATETIME()
            WHERE VendorListingId = @id AND AvailableQuantity >= @qty;
            IF @@ROWCOUNT <> 1 THROW 51404, 'Insufficient available stock.', 1;
            INSERT dbo.InventoryReservations (OrderId, VendorListingId, Quantity, Status, ExpiresAtUtc)
            VALUES (@OrderId, @id, @qty, 'Reserved', @ExpiresAtUtc);
            INSERT dbo.InventoryTransactions (VendorListingId, OnHandDelta, ReservedDelta, SourceType, SourceId, SourceLineId)
            VALUES (@id, 0, @qty, 'Reserve', @OrderId, @id);
        END;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

-- Caller must first confirm payment or conclusively verify non-payment.
-- Expiry is NOT proof of payment failure. No background auto-release here.
CREATE OR ALTER PROCEDURE dbo.FinalizeOrderInventory
    @OrderId int,
    @Outcome varchar(10),
    @Reason varchar(32)
AS
BEGIN
    SET ANSI_PADDING ON;
    SET ANSI_WARNINGS ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @OrderId IS NULL OR @OrderId <= 0 OR @Outcome IS NULL OR @Reason IS NULL
       OR NOT ((@Outcome = 'Consumed' AND @Reason = 'PaymentConfirmed')
        OR (@Outcome = 'Released' AND @Reason IN ('PaymentFailed','UnpaidCancellation','VerifiedExpiry')))
        THROW 51410, 'Invalid inventory outcome.', 1;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS (SELECT 1 FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE OrderId = @OrderId)
            THROW 51401, 'Order not found.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.InventoryReservations WHERE OrderId = @OrderId)
            THROW 51411, 'Reservation not found.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.InventoryReservations WHERE OrderId = @OrderId AND Status <> @Outcome)
        BEGIN
            -- A completed outcome is retryable, even after its deadline.
            COMMIT TRANSACTION;
            RETURN;
        END;
        IF EXISTS (SELECT 1 FROM dbo.InventoryReservations WHERE OrderId = @OrderId AND Status <> 'Reserved')
            THROW 51412, 'Reservation already finalized differently.', 1;
        IF @Reason = 'VerifiedExpiry' AND EXISTS
            (SELECT 1 FROM dbo.InventoryReservations WHERE OrderId = @OrderId AND ExpiresAtUtc > SYSUTCDATETIME())
            THROW 51413, 'Reservation has not expired.', 1;

        DECLARE @id int = 0, @qty bigint, @delta bigint, @source varchar(32);
        SET @source = CASE WHEN @Outcome = 'Consumed' THEN 'Consume' ELSE 'Release' END;
        WHILE 1 = 1
        BEGIN
            SELECT @id = MIN(VendorListingId) FROM dbo.InventoryReservations WHERE OrderId = @OrderId AND VendorListingId > @id;
            IF @id IS NULL BREAK;
            SELECT @qty = Quantity FROM dbo.InventoryReservations WHERE OrderId = @OrderId AND VendorListingId = @id;
            SET @delta = CASE WHEN @Outcome = 'Consumed' THEN -@qty ELSE 0 END;
            UPDATE dbo.InventoryBalances SET OnHandQuantity = OnHandQuantity + @delta,
                ReservedQuantity = ReservedQuantity - @qty, UpdatedAtUtc = SYSUTCDATETIME()
            WHERE VendorListingId = @id AND ReservedQuantity >= @qty AND OnHandQuantity >= @qty;
            IF @@ROWCOUNT <> 1 THROW 51414, 'Inventory balance inconsistency.', 1;
            UPDATE dbo.InventoryReservations SET Status = @Outcome, UpdatedAtUtc = SYSUTCDATETIME()
            WHERE OrderId = @OrderId AND VendorListingId = @id AND Status = 'Reserved';
            IF @@ROWCOUNT <> 1 THROW 51412, 'Reservation changed.', 1;
            INSERT dbo.InventoryTransactions (VendorListingId, OnHandDelta, ReservedDelta, SourceType, SourceId, SourceLineId, Reason)
            VALUES (@id, @delta, -@qty, @source, @OrderId, @id, @Reason);
        END;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
