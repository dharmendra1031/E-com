:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 52720,'Disposable database required.',1;
GO
CREATE TABLE #OpeningBatch(VendorId int NOT NULL,ActorId int NOT NULL,CountReference nvarchar(200) NOT NULL);
CREATE TABLE #OpeningLines(VendorListingId int NOT NULL,Quantity bigint NOT NULL);
INSERT #OpeningBatch SELECT VendorId,3,N'Fixture physical count' FROM dbo.Vendors WHERE VendorCode=N'FIRST-PARTY-FIXTURE';
INSERT #OpeningLines SELECT VendorListingId,CASE WHEN ProductId=1 THEN 7 ELSE 2 END
    FROM dbo.VendorListings WHERE VendorId=(SELECT VendorId FROM #OpeningBatch);
-- A single unrelated listing rejects the whole batch.
INSERT #OpeningLines SELECT TOP(1) l.VendorListingId,1 FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId WHERE v.VendorCode=N'B';
GO
BEGIN TRY
:r inventory/06-post-opening-stock.sql
    THROW 52721,'Cross-vendor opening accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52705 THROW;
END CATCH;
GO
IF EXISTS(SELECT 1 FROM dbo.InventoryBalances b JOIN dbo.VendorListings l ON l.VendorListingId=b.VendorListingId
    WHERE l.VendorId=(SELECT VendorId FROM #OpeningBatch)) THROW 52722,'Rejected batch posted stock.',1;
DELETE x FROM #OpeningLines x JOIN dbo.VendorListings l ON l.VendorListingId=x.VendorListingId WHERE l.VendorId<>(SELECT VendorId FROM #OpeningBatch);
UPDATE #OpeningBatch SET ActorId=2;
GO
BEGIN TRY
:r inventory/06-post-opening-stock.sql
    THROW 52723,'Non-admin opening accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52703 THROW;
END CATCH;
GO
UPDATE #OpeningBatch SET ActorId=3;
GO
:r inventory/06-post-opening-stock.sql
GO
:r inventory/06-post-opening-stock.sql
GO
IF (SELECT COUNT(*) FROM dbo.InventoryTransactions t JOIN #OpeningLines x ON t.VendorListingId=x.VendorListingId WHERE t.SourceType='OpeningStock')<>2
    OR EXISTS(SELECT 1 FROM #OpeningLines x LEFT JOIN dbo.InventoryBalances b ON b.VendorListingId=x.VendorListingId
        WHERE b.VendorListingId IS NULL OR b.OnHandQuantity<>x.Quantity OR b.ReservedQuantity<>0)
    OR EXISTS(SELECT 1 FROM dbo.VendorListings WHERE VendorId=(SELECT VendorId FROM #OpeningBatch) AND Status<>'Pending')
    THROW 52724,'Opening balance, movement count or listing state incorrect.',1;
UPDATE #OpeningLines SET Quantity=Quantity+1;
GO
BEGIN TRY
:r inventory/06-post-opening-stock.sql
    THROW 52725,'Changed opening retry accepted.',1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>52706 THROW;
END CATCH;
GO
IF EXISTS(SELECT 1 FROM #OpeningLines x JOIN dbo.InventoryBalances b ON b.VendorListingId=x.VendorListingId WHERE b.OnHandQuantity<>x.Quantity-1)
    THROW 52726,'Rejected retry changed stock.',1;
DROP TABLE #OpeningBatch;
DROP TABLE #OpeningLines;
PRINT 'First-party opening stock checks passed.';
GO
