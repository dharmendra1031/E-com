-- One-off first-party catalog staging. Run on a restored database only.
-- Caller must explicitly populate #FirstPartyVendor (one approved VendorId)
-- and #FirstPartyListings (ProductId, SellerSku, Price) with reviewed values.
-- Never infer the vendor, SKU, prices, balances or commercial approval.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF @@TRANCOUNT <> 0 THROW 52600,'Run staging outside another transaction.',1;
IF OBJECT_ID(N'tempdb..#FirstPartyVendor') IS NULL OR OBJECT_ID(N'tempdb..#FirstPartyListings') IS NULL
    THROW 52601,'Explicit approved mapping input required.',1;
IF (SELECT COUNT_BIG(*) FROM #FirstPartyVendor) <> 1
    THROW 52601,'Select exactly one existing first-party vendor.',1;
DECLARE @VendorId int = (SELECT VendorId FROM #FirstPartyVendor);
IF NOT EXISTS(SELECT 1 FROM #FirstPartyListings)
    THROW 52601,'No reviewed product mappings supplied.',1;
IF EXISTS(SELECT 1 FROM #FirstPartyListings WHERE ProductId IS NULL OR SellerSku IS NULL
    OR LEN(LTRIM(RTRIM(SellerSku)))=0 OR DATALENGTH(SellerSku)>200 OR Price IS NULL OR Price<0
    OR Price>999999999999999.999 OR Price<>ROUND(Price,3))
    THROW 52602,'Invalid product, SKU or price in mapping input.',1;
IF EXISTS(SELECT ProductId FROM #FirstPartyListings GROUP BY ProductId HAVING COUNT_BIG(*)>1)
    OR EXISTS(SELECT LTRIM(RTRIM(SellerSku)) COLLATE DATABASE_DEFAULT FROM #FirstPartyListings
        GROUP BY LTRIM(RTRIM(SellerSku)) COLLATE DATABASE_DEFAULT HAVING COUNT_BIG(*)>1)
    THROW 52603,'Duplicate product or seller SKU in mapping input.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS(SELECT 1 FROM dbo.Vendors WITH(UPDLOCK,HOLDLOCK)
        WHERE VendorId=@VendorId AND Status='Approved')
        THROW 52604,'Selected vendor must exist and be approved.',1;
    IF EXISTS(SELECT 1 FROM #FirstPartyListings m LEFT JOIN dbo.Products p WITH(HOLDLOCK)
        ON p.ProductId=m.ProductId WHERE p.ProductId IS NULL OR p.IsActive<>1)
        THROW 52605,'Mapping contains missing or inactive product.',1;
    IF EXISTS(SELECT 1 FROM #FirstPartyListings m JOIN dbo.VendorListings l WITH(UPDLOCK,HOLDLOCK)
        ON l.VendorId=@VendorId AND l.ProductId=m.ProductId
        WHERE l.SellerSku<>LTRIM(RTRIM(m.SellerSku)) COLLATE DATABASE_DEFAULT OR l.Price<>m.Price)
        THROW 52606,'Existing product mapping differs. No listing overwritten.',1;
    IF EXISTS(SELECT 1 FROM #FirstPartyListings m JOIN dbo.VendorListings l WITH(UPDLOCK,HOLDLOCK)
        ON l.VendorId=@VendorId AND l.SellerSku=LTRIM(RTRIM(m.SellerSku)) COLLATE DATABASE_DEFAULT
        WHERE l.ProductId<>m.ProductId)
        THROW 52607,'Seller SKU already belongs to another product.',1;
    INSERT dbo.VendorListings(VendorId,ProductId,SellerSku,Price,Status)
    SELECT @VendorId,m.ProductId,LTRIM(RTRIM(m.SellerSku)),m.Price,'Pending'
    FROM #FirstPartyListings m
    WHERE NOT EXISTS(SELECT 1 FROM dbo.VendorListings l WITH(UPDLOCK,HOLDLOCK)
        WHERE l.VendorId=@VendorId AND l.ProductId=m.ProductId);
    DECLARE @Inserted int=@@ROWCOUNT;
    COMMIT TRANSACTION;
    SELECT @VendorId AS VendorId,@Inserted AS NewPendingListings,
        (SELECT COUNT_BIG(*) FROM #FirstPartyListings)-@Inserted AS ExistingExactMappings;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
