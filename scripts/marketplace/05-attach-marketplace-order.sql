SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- Internal only. Caller creates the unpaid parent order in the SAME outer transaction.
-- No public route until customer, address, delivery, tax and payment rules are approved.
CREATE OR ALTER PROCEDURE dbo.AttachMarketplaceOrder
    @OrderId int,
    @CustomerId int,
    @ExpectedLines nvarchar(max),
    @ExpiresAtUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET ANSI_PADDING ON;
    SET ANSI_WARNINGS ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;
    IF @@TRANCOUNT = 0 THROW 51710, 'Outer order transaction required.', 1;
    BEGIN TRY
        IF @ExpectedLines IS NULL OR DATALENGTH(@ExpectedLines)>100000 OR ISJSON(@ExpectedLines)<>1 OR LEFT(LTRIM(@ExpectedLines),1)<>'['
            THROW 51711,'Invalid quote.',1;
        DECLARE @quote TABLE(ListingId int NULL, Quantity int NULL, Price decimal(18,3) NULL);
        INSERT @quote SELECT ListingId,Quantity,Price FROM OPENJSON(@ExpectedLines)
            WITH(ListingId int '$.listingId',Quantity int '$.quantity',Price decimal(18,3) '$.price');
        IF NOT EXISTS(SELECT 1 FROM @quote) OR (SELECT COUNT(*) FROM @quote)>100
           OR EXISTS(SELECT 1 FROM @quote WHERE ListingId IS NULL OR ListingId<=0 OR Quantity IS NULL OR Quantity<=0 OR Quantity>1000000 OR Price IS NULL OR Price<0)
           OR EXISTS(SELECT 1 FROM @quote GROUP BY ListingId HAVING COUNT(*)>1)
            THROW 51711,'Invalid or duplicate quote lines.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@OrderId
                      AND CustomerId=@CustomerId AND MarketplaceRequestId IS NOT NULL AND Status=1 AND PaymentStatus=2)
            THROW 51712,'Unpaid marketplace parent order required.',1;
        IF EXISTS(SELECT 1 FROM dbo.OrderItems WHERE OrderId=@OrderId)
            THROW 51713,'Parent already contains items; resolve retry by request ID.',1;

        -- Match complete customer cart; never accept posted seller identity, product or selling price as truth.
        IF EXISTS(SELECT ListingId,Quantity,Price FROM @quote EXCEPT
                  SELECT VendorListingId,Quantity,Price FROM dbo.CartItems WITH(UPDLOCK,HOLDLOCK) WHERE UserId=@CustomerId AND VendorListingId IS NOT NULL)
           OR EXISTS(SELECT VendorListingId,Quantity,Price FROM dbo.CartItems WITH(UPDLOCK,HOLDLOCK) WHERE UserId=@CustomerId AND VendorListingId IS NOT NULL
                     EXCEPT SELECT ListingId,Quantity,Price FROM @quote)
            THROW 51714,'Cart changed; review current quantities and prices.',1;

        DECLARE @lines TABLE(ListingId int PRIMARY KEY, VendorId int, ProductId int, Quantity int,
            Price decimal(18,3), SellerSku nvarchar(100), ProductName nvarchar(max), ProductNameArb nvarchar(max));
        INSERT @lines
        SELECT l.VendorListingId,l.VendorId,l.ProductId,q.Quantity,l.Price,l.SellerSku,p.Name,p.NameArb
        FROM @quote q JOIN dbo.VendorListings l WITH(UPDLOCK,HOLDLOCK) ON l.VendorListingId=q.ListingId
        JOIN dbo.Vendors v WITH(HOLDLOCK) ON v.VendorId=l.VendorId
        JOIN dbo.Products p WITH(HOLDLOCK) ON p.ProductId=l.ProductId
        WHERE l.Status='Approved' AND v.Status='Approved' AND p.IsActive=1 AND l.Price=q.Price;
        IF (SELECT COUNT(*) FROM @lines)<>(SELECT COUNT(*) FROM @quote)
            THROW 51715,'Seller offer or price changed.',1;

        DECLARE @subtotal decimal(38,3)=(SELECT SUM(CONVERT(decimal(38,3),Price*Quantity)) FROM @lines);
        -- No coupons or vendor allocation assumptions. Parent charges are trusted server-side quote snapshots.
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WHERE OrderId=@OrderId AND COALESCE(DiscountAmount,0)=0
            AND DeliveryCharge>=0 AND VatAmount>=0 AND VatPercent BETWEEN 0 AND 100
            AND VatAmount=ROUND(@subtotal*VatPercent/100,3)
            AND TotalAmount=@subtotal+VatAmount+DeliveryCharge)
            THROW 51716,'Parent quote totals do not reconcile.',1;
        INSERT dbo.VendorOrders(OrderId,VendorId,VendorName,VendorNameArb,MerchandiseSubtotal)
        SELECT @OrderId,v.VendorId,v.Name,v.NameArb,SUM(l.Price*l.Quantity)
        FROM @lines l JOIN dbo.Vendors v ON v.VendorId=l.VendorId GROUP BY v.VendorId,v.Name,v.NameArb;
        INSERT dbo.OrderItems(OrderId,ProductId,Quantity,Price,VendorOrderId,VendorListingId,SellerSkuSnapshot,ProductNameSnapshot,ProductNameArbSnapshot)
        SELECT @OrderId,l.ProductId,l.Quantity,l.Price,o.VendorOrderId,l.ListingId,l.SellerSku,l.ProductName,l.ProductNameArb
        FROM @lines l JOIN dbo.VendorOrders o ON o.OrderId=@OrderId AND o.VendorId=l.VendorId;
        DECLARE @reservation nvarchar(max)=(SELECT ListingId AS listingId,Quantity AS quantity FROM @lines FOR JSON PATH);
        EXEC dbo.ReserveOrderInventory @OrderId,@reservation,@ExpiresAtUtc;
        DELETE dbo.CartItems WHERE UserId=@CustomerId AND VendorListingId IN (SELECT ListingId FROM @lines);
        IF @@ROWCOUNT<>(SELECT COUNT(*) FROM @lines) THROW 51714,'Cart changed.',1;
        -- Caller commits parent + suborders + snapshots + reservation together.
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
