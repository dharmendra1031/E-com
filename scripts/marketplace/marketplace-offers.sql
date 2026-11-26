CREATE OR ALTER VIEW dbo.MarketplaceAvailableOffers
AS
SELECT l.VendorListingId AS ListingId, l.ProductId, v.Name AS VendorName,
       v.NameArb AS VendorNameArb, l.Price, b.AvailableQuantity
FROM dbo.VendorListings l
JOIN dbo.Vendors v ON v.VendorId = l.VendorId
JOIN dbo.Products p ON p.ProductId = l.ProductId
JOIN dbo.InventoryBalances b ON b.VendorListingId = l.VendorListingId
WHERE l.Status = 'Approved' AND v.Status = 'Approved' AND p.IsActive = 1
  AND b.AvailableQuantity > 0;
GO
