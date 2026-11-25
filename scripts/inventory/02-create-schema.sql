-- Additive migration. No backfill and no application cutover.
-- Run on a restored backup first, then only as part of an approved release.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL OR OBJECT_ID(N'dbo.Orders', N'U') IS NULL
    THROW 51001, 'Expected Products and Orders tables are missing.', 1;

DECLARE @Existing int =
    CASE WHEN OBJECT_ID(N'dbo.InventoryBalances', N'U') IS NOT NULL THEN 1 ELSE 0 END +
    CASE WHEN OBJECT_ID(N'dbo.InventoryReservations', N'U') IS NOT NULL THEN 1 ELSE 0 END +
    CASE WHEN OBJECT_ID(N'dbo.InventoryTransactions', N'U') IS NOT NULL THEN 1 ELSE 0 END;

IF @Existing = 3
BEGIN
    PRINT 'Inventory tables already exist. No schema or data changes made; verify schema version before cutover.';
    RETURN;
END;
IF @Existing <> 0
    THROW 51002, 'Partial inventory schema exists. Review before continuing.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE dbo.InventoryBalances (
        ProductId int NOT NULL CONSTRAINT PK_InventoryBalances PRIMARY KEY,
        OnHandQuantity bigint NOT NULL,
        ReservedQuantity bigint NOT NULL CONSTRAINT DF_InventoryBalances_Reserved DEFAULT (0),
        AvailableQuantity AS (OnHandQuantity - ReservedQuantity),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_InventoryBalances_Updated DEFAULT SYSUTCDATETIME(),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_InventoryBalances_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
        CONSTRAINT CK_InventoryBalances_Quantity CHECK (OnHandQuantity >= 0 AND ReservedQuantity >= 0 AND ReservedQuantity <= OnHandQuantity)
    );

    CREATE TABLE dbo.InventoryReservations (
        OrderId int NOT NULL,
        ProductId int NOT NULL,
        Quantity bigint NOT NULL,
        Status varchar(10) NOT NULL,
        ExpiresAtUtc datetime2(3) NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_InventoryReservations_Created DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_InventoryReservations_Updated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_InventoryReservations PRIMARY KEY (OrderId, ProductId),
        CONSTRAINT FK_InventoryReservations_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId),
        CONSTRAINT FK_InventoryReservations_Balances FOREIGN KEY (ProductId) REFERENCES dbo.InventoryBalances(ProductId),
        CONSTRAINT CK_InventoryReservations_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_InventoryReservations_Status CHECK (Status IN ('Reserved', 'Consumed', 'Released')),
        CONSTRAINT CK_InventoryReservations_Expiry CHECK (ExpiresAtUtc > CreatedAtUtc)
    );
    CREATE INDEX IX_InventoryReservations_Expiry ON dbo.InventoryReservations (Status, ExpiresAtUtc) INCLUDE (ProductId, Quantity);

    CREATE TABLE dbo.InventoryTransactions (
        InventoryTransactionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventoryTransactions PRIMARY KEY,
        ProductId int NOT NULL,
        OnHandDelta bigint NOT NULL,
        ReservedDelta bigint NOT NULL,
        SourceType varchar(32) NOT NULL,
        SourceId bigint NOT NULL,
        SourceLineId bigint NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_InventoryTransactions_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy int NULL,
        CONSTRAINT FK_InventoryTransactions_Balances FOREIGN KEY (ProductId) REFERENCES dbo.InventoryBalances(ProductId),
        CONSTRAINT CK_InventoryTransactions_Delta CHECK (OnHandDelta <> 0 OR ReservedDelta <> 0),
        CONSTRAINT UQ_InventoryTransactions_Source UNIQUE (ProductId, SourceType, SourceId, SourceLineId)
    );

    COMMIT TRANSACTION;
    PRINT 'Inventory foundation created. Tables intentionally empty; existing stock and orders unchanged.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
