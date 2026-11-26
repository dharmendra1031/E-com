-- Additive: apply BEFORE deploying new application binaries, even when flag OFF.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.CartItems',N'U') IS NULL OR OBJECT_ID(N'dbo.VendorListings',N'U') IS NULL
    THROW 51600, 'Cart and listing schema required.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH(N'dbo.CartItems',N'VendorListingId') IS NULL
        ALTER TABLE dbo.CartItems ADD VendorListingId int NULL;
    IF OBJECT_ID(N'dbo.FK_CartItems_VendorListings',N'F') IS NULL
        EXEC(N'ALTER TABLE dbo.CartItems WITH CHECK ADD CONSTRAINT FK_CartItems_VendorListings FOREIGN KEY (VendorListingId) REFERENCES dbo.VendorListings(VendorListingId)');
    IF OBJECT_ID(N'dbo.CK_CartItems_SellerLine',N'C') IS NULL
        EXEC(N'ALTER TABLE dbo.CartItems WITH CHECK ADD CONSTRAINT CK_CartItems_SellerLine CHECK (VendorListingId IS NULL OR (UserId IS NOT NULL AND UserId > 0 AND Quantity > 0 AND Quantity <= 1000000 AND Price >= 0))');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CartItems') AND name = N'UX_CartItems_UserListing')
        EXEC(N'CREATE UNIQUE INDEX UX_CartItems_UserListing ON dbo.CartItems(UserId, VendorListingId) WHERE VendorListingId IS NOT NULL');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
