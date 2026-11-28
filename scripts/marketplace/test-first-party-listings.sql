:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 52620,'Disposable database required.',1;
GO
INSERT dbo.Vendors(VendorCode,Name,Status) VALUES(N'FIRST-PARTY-FIXTURE',N'Fixture first party','Approved');
CREATE TABLE #FirstPartyVendor(VendorId int NOT NULL);
CREATE TABLE #FirstPartyListings(ProductId int NOT NULL,SellerSku nvarchar(100) NOT NULL,Price decimal(18,3) NOT NULL);
INSERT #FirstPartyVendor SELECT VendorId FROM dbo.Vendors WHERE VendorCode=N'FIRST-PARTY-FIXTURE';
INSERT #FirstPartyListings VALUES(1,N'FIRST-1',101.000),(2,N'FIRST-2',25.000);
GO
:r 13-stage-first-party-listings.sql
GO
:r 13-stage-first-party-listings.sql
GO
DECLARE @VendorId int=(SELECT VendorId FROM #FirstPartyVendor);
IF (SELECT COUNT(*) FROM dbo.VendorListings WHERE VendorId=@VendorId)<>2
    OR EXISTS(SELECT 1 FROM dbo.VendorListings WHERE VendorId=@VendorId AND Status<>'Pending')
    OR EXISTS(SELECT 1 FROM dbo.InventoryBalances b JOIN dbo.VendorListings l ON l.VendorListingId=b.VendorListingId WHERE l.VendorId=@VendorId)
    OR EXISTS(SELECT 1 FROM dbo.InventoryTransactions t JOIN dbo.VendorListings l ON l.VendorListingId=t.VendorListingId WHERE l.VendorId=@VendorId)
    THROW 52621,'Staging duplicated listings, approved sales or created stock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId WHERE v.VendorCode=N'B' AND l.ProductId=1)
    THROW 52622,'Existing seller listing missing.',1;
GO
UPDATE #FirstPartyListings SET Price=102 WHERE ProductId=1;
GO
BEGIN TRY
:r 13-stage-first-party-listings.sql
    THROW 52623,'Changed mapping accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52606 THROW;
END CATCH;
GO
UPDATE #FirstPartyListings SET Price=101 WHERE ProductId=1;
UPDATE #FirstPartyListings SET SellerSku=N'FIRST-1' WHERE ProductId=2;
GO
BEGIN TRY
:r 13-stage-first-party-listings.sql
    THROW 52624,'Duplicate SKU accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52603 THROW;
END CATCH;
GO
UPDATE #FirstPartyListings SET SellerSku=N'FIRST-2' WHERE ProductId=2;
UPDATE #FirstPartyListings SET ProductId=999999 WHERE ProductId=2;
GO
BEGIN TRY
:r 13-stage-first-party-listings.sql
    THROW 52625,'Unknown product accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52605 THROW;
END CATCH;
GO
IF (SELECT COUNT(*) FROM dbo.VendorListings WHERE VendorId=(SELECT VendorId FROM #FirstPartyVendor))<>2
    OR NOT EXISTS(SELECT 1 FROM dbo.VendorListings WHERE VendorId=(SELECT VendorId FROM #FirstPartyVendor) AND ProductId=1 AND Price=101.000)
    THROW 52626,'Rejected mapping changed stored listings.',1;
DROP TABLE #FirstPartyListings;
DROP TABLE #FirstPartyVendor;
PRINT 'First-party catalog staging checks passed.';
GO
