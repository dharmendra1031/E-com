-- Upgrade ONLY the unused staged inventory foundation. Never discard posted balances or movements.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.VendorListings', N'U') IS NULL
    OR OBJECT_ID(N'dbo.InventoryBalances', N'U') IS NULL
    OR OBJECT_ID(N'dbo.InventoryReservations', N'U') IS NULL
    OR OBJECT_ID(N'dbo.InventoryTransactions', N'U') IS NULL
    THROW 51300, 'Apply marketplace and inventory foundation first.', 1;
IF COL_LENGTH(N'dbo.InventoryBalances', N'VendorListingId') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.InventoryReservations', N'VendorListingId') IS NULL
       OR COL_LENGTH(N'dbo.InventoryTransactions', N'VendorListingId') IS NULL
       OR COL_LENGTH(N'dbo.InventoryTransactions', N'RequestId') IS NULL
        THROW 51301, 'Partial listing inventory schema.', 1;
    PRINT 'Listing inventory already installed; verify installed definitions.';
    RETURN;
END;
IF COL_LENGTH(N'dbo.InventoryBalances', N'ProductId') IS NULL
    OR COL_LENGTH(N'dbo.InventoryReservations', N'ProductId') IS NULL
    OR COL_LENGTH(N'dbo.InventoryTransactions', N'ProductId') IS NULL
    THROW 51301, 'Unexpected existing inventory schema.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM dbo.InventoryTransactions WITH (TABLOCKX, HOLDLOCK))
       OR EXISTS (SELECT 1 FROM dbo.InventoryReservations WITH (TABLOCKX, HOLDLOCK))
       OR EXISTS (SELECT 1 FROM dbo.InventoryBalances WITH (TABLOCKX, HOLDLOCK))
        THROW 51302, 'Inventory has data. Reconciliation and a data-preserving migration are required.', 1;

    DROP TABLE dbo.InventoryTransactions;
    DROP TABLE dbo.InventoryReservations;
    DROP TABLE dbo.InventoryBalances;

    EXEC sys.sp_executesql N'    CREATE TABLE dbo.InventoryBalances (
        VendorListingId int NOT NULL CONSTRAINT PK_InventoryBalances PRIMARY KEY,
        OnHandQuantity bigint NOT NULL,
        ReservedQuantity bigint NOT NULL CONSTRAINT DF_InventoryBalances_Reserved DEFAULT (0),
        AvailableQuantity AS (OnHandQuantity - ReservedQuantity),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_InventoryBalances_Updated DEFAULT SYSUTCDATETIME(),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_InventoryBalances_Listings FOREIGN KEY (VendorListingId) REFERENCES dbo.VendorListings(VendorListingId),
        CONSTRAINT CK_InventoryBalances_Quantity CHECK (OnHandQuantity >= 0 AND ReservedQuantity >= 0 AND ReservedQuantity <= OnHandQuantity)
    );';

    EXEC sys.sp_executesql N'    CREATE TABLE dbo.InventoryReservations (
        OrderId int NOT NULL,
        VendorListingId int NOT NULL,
        Quantity bigint NOT NULL,
        Status varchar(10) NOT NULL,
        ExpiresAtUtc datetime2(3) NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_InventoryReservations_Created DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_InventoryReservations_Updated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_InventoryReservations PRIMARY KEY (OrderId, VendorListingId),
        CONSTRAINT FK_InventoryReservations_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId),
        CONSTRAINT FK_InventoryReservations_Balances FOREIGN KEY (VendorListingId) REFERENCES dbo.InventoryBalances(VendorListingId),
        CONSTRAINT CK_InventoryReservations_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_InventoryReservations_Status CHECK (Status IN (''Reserved'', ''Consumed'', ''Released'')),
        CONSTRAINT CK_InventoryReservations_Expiry CHECK (ExpiresAtUtc > CreatedAtUtc)
    );
    CREATE INDEX IX_InventoryReservations_Expiry ON dbo.InventoryReservations (Status, ExpiresAtUtc) INCLUDE (VendorListingId, Quantity);';

    EXEC sys.sp_executesql N'    CREATE TABLE dbo.InventoryTransactions (
        InventoryTransactionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventoryTransactions PRIMARY KEY,
        VendorListingId int NOT NULL,
        OnHandDelta bigint NOT NULL,
        ReservedDelta bigint NOT NULL,
        SourceType varchar(32) NOT NULL,
        SourceId bigint NOT NULL,
        SourceLineId bigint NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_InventoryTransactions_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy int NULL,
        RequestId uniqueidentifier NULL,
        Reason nvarchar(200) NULL,
        CONSTRAINT FK_InventoryTransactions_Balances FOREIGN KEY (VendorListingId) REFERENCES dbo.InventoryBalances(VendorListingId),
        CONSTRAINT CK_InventoryTransactions_Delta CHECK (OnHandDelta <> 0 OR ReservedDelta <> 0),
        CONSTRAINT UQ_InventoryTransactions_Source UNIQUE (VendorListingId, SourceType, SourceId, SourceLineId)
    );';

    EXEC sys.sp_executesql N'    CREATE UNIQUE INDEX UX_InventoryTransactions_Request ON dbo.InventoryTransactions(RequestId) WHERE RequestId IS NOT NULL;
    CREATE INDEX IX_InventoryTransactions_Listing ON dbo.InventoryTransactions(VendorListingId, InventoryTransactionId DESC);';

    COMMIT TRANSACTION;
    PRINT 'Empty staged inventory re-keyed to seller listings.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
