-- Disposable SQL Server CI database only. Do not run against a live database.
:ON ERROR EXIT
IF DB_NAME() <> N'InventoryMigrationTest'
    THROW 51010, 'This test requires the disposable InventoryMigrationTest database.', 1;
GO
CREATE TABLE dbo.Products (ProductId int NOT NULL PRIMARY KEY, Stock int NULL);
CREATE TABLE dbo.Orders (OrderId int NOT NULL PRIMARY KEY, Status int NOT NULL, PaymentStatus int NOT NULL);
CREATE TABLE dbo.ProductStock (ID int IDENTITY PRIMARY KEY, ProductId int NULL, StockQty int NULL);
CREATE TABLE dbo.OrderItems (OrderItemId int IDENTITY PRIMARY KEY, OrderId int NULL, ProductId int NULL, Quantity int NULL);
INSERT dbo.Products VALUES (1, 10), (2, 0);
INSERT dbo.Orders VALUES (1, 1, 2), (2, 1, 2);
INSERT dbo.ProductStock (ProductId, StockQty) VALUES (1, 10);
INSERT dbo.OrderItems (OrderId, ProductId, Quantity) VALUES (1, 1, 2);
GO
:r 01-reconcile.sql
GO
:r 02-create-schema.sql
GO
:r 02-create-schema.sql
GO
IF EXISTS (SELECT 1 FROM dbo.InventoryBalances)
    THROW 51011, 'Migration must not seed unreconciled balances.', 1;
IF (SELECT Stock FROM dbo.Products WHERE ProductId = 1) <> 10
   OR (SELECT SUM(StockQty) FROM dbo.ProductStock) <> 10
   OR (SELECT SUM(Quantity) FROM dbo.OrderItems) <> 2
    THROW 51012, 'Legacy data was changed.', 1;

INSERT dbo.InventoryBalances (ProductId, OnHandQuantity) VALUES (1, 8);
SET XACT_ABORT OFF;
BEGIN TRY
    UPDATE dbo.InventoryBalances SET ReservedQuantity = 9 WHERE ProductId = 1;
    THROW 51013, 'Invalid reservation was accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 547 THROW;
END CATCH;

-- Illustrate the conditional update required by the future application integration.
BEGIN TRANSACTION;
UPDATE dbo.InventoryBalances SET ReservedQuantity = ReservedQuantity + 5
WHERE ProductId = 1 AND AvailableQuantity >= 5;
IF @@ROWCOUNT <> 1 THROW 51014, 'Expected reservation was rejected.', 1;
INSERT dbo.InventoryReservations (OrderId, ProductId, Quantity, Status, ExpiresAtUtc)
VALUES (1, 1, 5, 'Reserved', DATEADD(minute, 15, SYSUTCDATETIME()));
INSERT dbo.InventoryTransactions (ProductId, OnHandDelta, ReservedDelta, SourceType, SourceId, SourceLineId)
VALUES (1, 0, 5, 'Reserve', 1, 1);
UPDATE dbo.InventoryBalances SET ReservedQuantity = ReservedQuantity + 5
WHERE ProductId = 1 AND AvailableQuantity >= 5;
IF @@ROWCOUNT <> 0 THROW 51015, 'Insufficient stock was accepted.', 1;
COMMIT TRANSACTION;

BEGIN TRY
    INSERT dbo.InventoryTransactions (ProductId, OnHandDelta, ReservedDelta, SourceType, SourceId, SourceLineId)
    VALUES (1, 0, 5, 'Reserve', 1, 1);
    THROW 51016, 'Duplicate source was accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
END CATCH;
IF (SELECT AvailableQuantity FROM dbo.InventoryBalances WHERE ProductId = 1) <> 3
    THROW 51017, 'Incorrect available balance.', 1;
PRINT 'PASS: diagnostic query, additive migration, repeat migration, legacy preservation, quantity constraint, conditional update and duplicate source constraint.';
GO
