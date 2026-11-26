-- Marketplace stage 1: additive vendor and listing foundation.
-- Run on a restored SQL Server backup first. No seed, backfill or runtime cutover.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
    THROW 51101, 'Expected Products and Users tables are missing.', 1;

DECLARE @Existing int =
    CASE WHEN OBJECT_ID(N'dbo.Vendors', N'U') IS NOT NULL THEN 1 ELSE 0 END +
    CASE WHEN OBJECT_ID(N'dbo.VendorUsers', N'U') IS NOT NULL THEN 1 ELSE 0 END +
    CASE WHEN OBJECT_ID(N'dbo.VendorListings', N'U') IS NOT NULL THEN 1 ELSE 0 END;

IF @Existing = 3
BEGIN
    PRINT 'Marketplace tables already exist; verify definitions before cutover.';
    RETURN;
END;
IF @Existing <> 0
    THROW 51102, 'Partial marketplace schema exists. Review before continuing.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE dbo.Vendors (
        VendorId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vendors PRIMARY KEY,
        VendorCode nvarchar(50) NOT NULL CONSTRAINT UQ_Vendors_Code UNIQUE,
        Name nvarchar(200) NOT NULL,
        NameArb nvarchar(200) NULL,
        Status varchar(12) NOT NULL CONSTRAINT DF_Vendors_Status DEFAULT ('Pending'),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Vendors_Created DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_Vendors_Status CHECK (Status IN ('Pending', 'Approved', 'Suspended')),
        CONSTRAINT CK_Vendors_Code CHECK (LEN(LTRIM(RTRIM(VendorCode))) > 0),
        CONSTRAINT CK_Vendors_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0)
    );

    CREATE TABLE dbo.VendorUsers (
        VendorId int NOT NULL,
        UserId int NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_VendorUsers_Active DEFAULT (1),
        CONSTRAINT PK_VendorUsers PRIMARY KEY (VendorId, UserId),
        CONSTRAINT FK_VendorUsers_Vendors FOREIGN KEY (VendorId) REFERENCES dbo.Vendors(VendorId),
        CONSTRAINT FK_VendorUsers_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
    );
    CREATE INDEX IX_VendorUsers_User ON dbo.VendorUsers (UserId, VendorId) INCLUDE (IsActive);

    CREATE TABLE dbo.VendorListings (
        VendorListingId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_VendorListings PRIMARY KEY,
        VendorId int NOT NULL,
        ProductId int NOT NULL,
        SellerSku nvarchar(100) NOT NULL,
        Price decimal(18,3) NOT NULL,
        Status varchar(12) NOT NULL CONSTRAINT DF_VendorListings_Status DEFAULT ('Pending'),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_VendorListings_Created DEFAULT SYSUTCDATETIME(),
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_VendorListings_Vendors FOREIGN KEY (VendorId) REFERENCES dbo.Vendors(VendorId),
        CONSTRAINT FK_VendorListings_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
        CONSTRAINT UQ_VendorListings_VendorSku UNIQUE (VendorId, SellerSku),
        CONSTRAINT UQ_VendorListings_VendorProduct UNIQUE (VendorId, ProductId),
        CONSTRAINT CK_VendorListings_Sku CHECK (LEN(LTRIM(RTRIM(SellerSku))) > 0),
        CONSTRAINT CK_VendorListings_Price CHECK (Price >= 0),
        CONSTRAINT CK_VendorListings_Status CHECK (Status IN ('Pending', 'Approved', 'Inactive'))
    );
    CREATE INDEX IX_VendorListings_Product ON dbo.VendorListings (ProductId, Status, Price, VendorListingId) INCLUDE (VendorId);

    COMMIT TRANSACTION;
    PRINT 'Marketplace foundation created. Existing stock, products, orders and users unchanged.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
