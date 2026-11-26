-- Synthetic data only. No production connection permitted.
:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceScaleTest' THROW 52200,'Use MarketplaceScaleTest only.',1;
IF OBJECT_ID(N'dbo.Products',N'U') IS NOT NULL THROW 52201,'New disposable database required.',1;
GO
SET NOCOUNT ON;
CREATE TABLE dbo.Products(ProductId int PRIMARY KEY,Name nvarchar(100) NOT NULL,CategoryId int NOT NULL,IsActive bit NOT NULL);
CREATE TABLE dbo.Users(UserId int PRIMARY KEY);
CREATE TABLE dbo.Orders(OrderId int PRIMARY KEY);
GO
:r marketplace/01-create-schema.sql
GO
:r inventory/02-create-schema.sql
GO
:r inventory/03-listing-scope.sql
GO
:r marketplace/marketplace-offers.sql
GO
SET NOCOUNT ON;
DECLARE @start datetime2(3)=SYSUTCDATETIME();
;WITH digits AS (SELECT n FROM (VALUES(0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) d(n)),
numbers AS (SELECT 1+a.n+10*b.n+100*c.n+1000*d.n+10000*e.n+100000*f.n AS id FROM digits a CROSS JOIN digits b CROSS JOIN digits c CROSS JOIN digits d CROSS JOIN digits e CROSS JOIN digits f)
INSERT dbo.Products(ProductId,Name,CategoryId,IsActive)
SELECT id,CONCAT(N'Benchmark product ',id),id%100+1,CASE WHEN id%10=0 THEN 0 ELSE 1 END FROM numbers;
INSERT dbo.Vendors(VendorCode,Name,Status) VALUES(N'BENCH-A',N'Benchmark A','Approved'),(N'BENCH-B',N'Benchmark B','Approved');
INSERT dbo.VendorListings(VendorId,ProductId,SellerSku,Price,Status)
SELECT v.VendorId,p.ProductId,CONCAT(N'SKU-',p.ProductId),
    10+CONVERT(decimal(18,3),p.ProductId%1000)/10+CASE WHEN v.VendorCode=N'BENCH-A' THEN 1 ELSE 0 END,
    CASE WHEN v.VendorCode=N'BENCH-B' AND p.ProductId%5=0 THEN 'Pending' ELSE 'Approved' END
FROM dbo.Products p CROSS JOIN dbo.Vendors v;
INSERT dbo.InventoryBalances(VendorListingId,OnHandQuantity,ReservedQuantity)
SELECT l.VendorListingId,CASE WHEN l.ProductId%7=0 THEN 0 ELSE 20 END,
    CASE WHEN l.ProductId%7=0 THEN 0 ELSE 5 END FROM dbo.VendorListings l;
IF (SELECT COUNT_BIG(*) FROM dbo.Products)<>1000000 OR (SELECT COUNT_BIG(*) FROM dbo.VendorListings)<>2000000
    THROW 52202,'Synthetic dataset count incorrect.',1;
SELECT 1000000 AS Products,2000000 AS Listings,DATEDIFF_BIG(millisecond,@start,SYSUTCDATETIME()) AS SeedMilliseconds;
UPDATE STATISTICS dbo.Products;
UPDATE STATISTICS dbo.VendorListings;
UPDATE STATISTICS dbo.InventoryBalances;
GO
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
DECLARE @start datetime2(3)=SYSUTCDATETIME();
SELECT ListingId,Price,AvailableQuantity FROM dbo.MarketplaceAvailableOffers WHERE ProductId=123456 ORDER BY Price,ListingId;
SELECT 'Single product offer lookup' AS Benchmark,DATEDIFF_BIG(millisecond,@start,SYSUTCDATETIME()) AS ElapsedMilliseconds;
IF (SELECT COUNT(*) FROM dbo.MarketplaceAvailableOffers WHERE ProductId=123456)<>2
    THROW 52203,'Expected two active offers.',1;
IF (SELECT MIN(Price) FROM dbo.MarketplaceAvailableOffers WHERE ProductId=123456)<>55.600
    THROW 52204,'Lowest price incorrect.',1;
SET @start=SYSUTCDATETIME();
;WITH summaries AS (SELECT ProductId,MIN(Price) LowestPrice,COUNT(*) SellerCount FROM dbo.MarketplaceAvailableOffers GROUP BY ProductId)
SELECT TOP(24) p.ProductId,s.LowestPrice,s.SellerCount FROM dbo.Products p
JOIN summaries s ON s.ProductId=p.ProductId
WHERE p.IsActive=1 AND p.CategoryId=57 AND s.LowestPrice BETWEEN 20 AND 60
ORDER BY s.LowestPrice,p.ProductId;
SELECT 'Category price filter and sort' AS Benchmark,DATEDIFF_BIG(millisecond,@start,SYSUTCDATETIME()) AS ElapsedMilliseconds;
SET @start=SYSUTCDATETIME();
;WITH summaries AS (SELECT ProductId,MIN(Price) LowestPrice,COUNT(*) SellerCount FROM dbo.MarketplaceAvailableOffers GROUP BY ProductId)
SELECT p.ProductId,s.LowestPrice,s.SellerCount FROM dbo.Products p
LEFT JOIN summaries s ON s.ProductId=p.ProductId
WHERE p.IsActive=1 ORDER BY CASE WHEN s.LowestPrice IS NULL THEN 1 ELSE 0 END,s.LowestPrice,p.ProductId
OFFSET 24000 ROWS FETCH NEXT 24 ROWS ONLY;
SELECT 'Deep catalog price pagination' AS Benchmark,DATEDIFF_BIG(millisecond,@start,SYSUTCDATETIME()) AS ElapsedMilliseconds;
SET @start=SYSUTCDATETIME();
SELECT COUNT_BIG(*) AS MatchingProducts FROM dbo.Products WHERE IsActive=1 AND Name LIKE N'%12345%';
SELECT 'Substring name search' AS Benchmark,DATEDIFF_BIG(millisecond,@start,SYSUTCDATETIME()) AS ElapsedMilliseconds;
SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
PRINT 'Marketplace synthetic scale query checks completed.';
GO
