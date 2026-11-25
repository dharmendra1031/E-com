-- READ ONLY against the existing business tables. Run on a restored backup or
-- during a write-free window. Results are diagnostic, not opening stock.
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
   OR OBJECT_ID(N'dbo.ProductStock', N'U') IS NULL
   OR OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Orders', N'U') IS NULL
    THROW 51000, 'Unexpected legacy schema. Verify actual table mappings first.', 1;

WITH receipts AS (
    SELECT ProductId, SUM(CONVERT(bigint, COALESCE(StockQty, 0))) AS ReceiptRowsQty
    FROM dbo.ProductStock GROUP BY ProductId
), sales AS (
    SELECT ProductId, SUM(CONVERT(bigint, COALESCE(Quantity, 0))) AS OrderRowsQty
    FROM dbo.OrderItems GROUP BY ProductId
)
SELECT p.ProductId, p.Stock AS ProductMasterStock,
       COALESCE(r.ReceiptRowsQty, 0) AS ReceiptRowsQty,
       COALESCE(s.OrderRowsQty, 0) AS OrderRowsQty,
       COALESCE(r.ReceiptRowsQty, 0) - COALESCE(s.OrderRowsQty, 0) AS LegacyCalculatedQty
INTO #InventoryDiagnostic
FROM dbo.Products p
LEFT JOIN receipts r ON r.ProductId = p.ProductId
LEFT JOIN sales s ON s.ProductId = p.ProductId;

SELECT COUNT_BIG(*) AS ProductCount,
       COUNT_BIG(CASE WHEN LegacyCalculatedQty < 0 THEN 1 END) AS NegativeCalculatedCount,
       COUNT_BIG(CASE WHEN ReceiptRowsQty < 0 THEN 1 END) AS NegativeReceiptCount,
       COUNT_BIG(CASE WHEN ProductMasterStock IS NULL OR ProductMasterStock <> LegacyCalculatedQty THEN 1 END) AS MasterMismatchCount
FROM #InventoryDiagnostic;

SELECT TOP (200) ProductId, ProductMasterStock, ReceiptRowsQty, OrderRowsQty, LegacyCalculatedQty
FROM #InventoryDiagnostic
WHERE ProductMasterStock IS NULL OR ProductMasterStock <> LegacyCalculatedQty OR LegacyCalculatedQty < 0 OR ReceiptRowsQty < 0
ORDER BY CASE WHEN LegacyCalculatedQty < 0 THEN 0 ELSE 1 END, ProductId;

SELECT 'Invalid receipt rows' AS Finding, COUNT_BIG(*) AS RowCount
FROM dbo.ProductStock s LEFT JOIN dbo.Products p ON p.ProductId = s.ProductId
WHERE p.ProductId IS NULL OR s.StockQty IS NULL OR s.StockQty < 0
UNION ALL
SELECT 'Invalid order items', COUNT_BIG(*)
FROM dbo.OrderItems i LEFT JOIN dbo.Products p ON p.ProductId = i.ProductId
LEFT JOIN dbo.Orders o ON o.OrderId = i.OrderId
WHERE p.ProductId IS NULL OR o.OrderId IS NULL OR i.Quantity IS NULL OR i.Quantity <= 0;

SELECT o.Status, o.PaymentStatus, COUNT_BIG(*) AS OrderCount
FROM dbo.Orders o GROUP BY o.Status, o.PaymentStatus ORDER BY o.Status, o.PaymentStatus;

-- Inventory-related database modules not represented in the application repository.
SELECT SCHEMA_NAME(o.schema_id) AS SchemaName, o.name AS ObjectName, o.type_desc
FROM sys.objects o INNER JOIN sys.sql_modules m ON m.object_id = o.object_id
WHERE m.definition LIKE N'%ProductStock%' OR m.definition LIKE N'%OrderItems%' OR m.definition LIKE N'%InventoryBalance%'
ORDER BY SchemaName, ObjectName;
DROP TABLE #InventoryDiagnostic;
