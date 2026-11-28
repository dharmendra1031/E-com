-- Additive staged marketplace order schema. Apply before updated application binaries.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL OR OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
   OR OBJECT_ID(N'dbo.VendorListings', N'U') IS NULL THROW 51700, 'Order and listing foundation required.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH(N'dbo.Orders', N'MarketplaceRequestId') IS NULL
        ALTER TABLE dbo.Orders ADD MarketplaceRequestId uniqueidentifier NULL;
    IF COL_LENGTH(N'dbo.Orders',N'MarketplaceDeliverySnapshot') IS NULL
        ALTER TABLE dbo.Orders ADD MarketplaceDeliverySnapshot nvarchar(max) NULL;
    IF OBJECT_ID(N'dbo.CK_Orders_MarketplaceDeliverySnapshot',N'C') IS NULL
        EXEC(N'ALTER TABLE dbo.Orders WITH CHECK ADD CONSTRAINT CK_Orders_MarketplaceDeliverySnapshot CHECK (MarketplaceDeliverySnapshot IS NULL OR (MarketplaceRequestId IS NOT NULL AND ISJSON(MarketplaceDeliverySnapshot)=1))');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Orders') AND name=N'UX_Orders_MarketplaceRequest')
        EXEC(N'CREATE UNIQUE INDEX UX_Orders_MarketplaceRequest ON dbo.Orders(CustomerId, MarketplaceRequestId) WHERE MarketplaceRequestId IS NOT NULL');
    IF OBJECT_ID(N'dbo.VendorOrders',N'U') IS NULL
        CREATE TABLE dbo.VendorOrders (
            VendorOrderId int IDENTITY PRIMARY KEY,
            OrderId int NOT NULL REFERENCES dbo.Orders(OrderId),
            VendorId int NOT NULL REFERENCES dbo.Vendors(VendorId),
            VendorName nvarchar(200) NOT NULL,
            VendorNameArb nvarchar(200) NULL,
            MerchandiseSubtotal decimal(18,3) NOT NULL CHECK (MerchandiseSubtotal >= 0),
            CONSTRAINT UQ_VendorOrders_OrderVendor UNIQUE(OrderId,VendorId),
            CONSTRAINT UQ_VendorOrders_OrderLink UNIQUE(OrderId,VendorOrderId)
        );
    IF COL_LENGTH(N'dbo.OrderItems',N'VendorOrderId') IS NULL
        ALTER TABLE dbo.OrderItems ADD VendorOrderId int NULL, VendorListingId int NULL,
            SellerSkuSnapshot nvarchar(100) NULL, ProductNameSnapshot nvarchar(max) NULL, ProductNameArbSnapshot nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.OrderItems',N'VendorListingId') IS NULL OR COL_LENGTH(N'dbo.OrderItems',N'SellerSkuSnapshot') IS NULL
       OR COL_LENGTH(N'dbo.OrderItems',N'ProductNameSnapshot') IS NULL OR COL_LENGTH(N'dbo.OrderItems',N'ProductNameArbSnapshot') IS NULL
        THROW 51701,'Partial order-item schema.',1;
    IF OBJECT_ID(N'dbo.FK_OrderItems_VendorOrder',N'F') IS NULL
        EXEC(N'ALTER TABLE dbo.OrderItems WITH CHECK ADD CONSTRAINT FK_OrderItems_VendorOrder FOREIGN KEY(OrderId,VendorOrderId) REFERENCES dbo.VendorOrders(OrderId,VendorOrderId)');
    IF OBJECT_ID(N'dbo.FK_OrderItems_VendorListing',N'F') IS NULL
        EXEC(N'ALTER TABLE dbo.OrderItems WITH CHECK ADD CONSTRAINT FK_OrderItems_VendorListing FOREIGN KEY(VendorListingId) REFERENCES dbo.VendorListings(VendorListingId)');
    IF OBJECT_ID(N'dbo.CK_OrderItems_Marketplace',N'C') IS NULL
        EXEC(N'ALTER TABLE dbo.OrderItems WITH CHECK ADD CONSTRAINT CK_OrderItems_Marketplace CHECK ((VendorOrderId IS NULL AND VendorListingId IS NULL) OR (VendorOrderId IS NOT NULL AND VendorListingId IS NOT NULL AND OrderId IS NOT NULL AND ProductId IS NOT NULL AND Quantity IS NOT NULL AND Quantity > 0 AND Price IS NOT NULL AND Price >= 0 AND SellerSkuSnapshot IS NOT NULL AND ProductNameSnapshot IS NOT NULL))');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.OrderItems') AND name=N'UX_OrderItems_OrderListing')
        EXEC(N'CREATE UNIQUE INDEX UX_OrderItems_OrderListing ON dbo.OrderItems(OrderId,VendorListingId) WHERE VendorListingId IS NOT NULL');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
