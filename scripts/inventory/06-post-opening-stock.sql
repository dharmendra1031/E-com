-- One-off, operator-reviewed opening stock on a restored database.
-- Reuse the existing listing balance and movement ledger, never infer stock from legacy totals.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF @@TRANCOUNT<>0 THROW 52700,'Run opening stock outside another transaction.',1;
IF OBJECT_ID(N'tempdb..#OpeningBatch') IS NULL OR OBJECT_ID(N'tempdb..#OpeningLines') IS NULL
    THROW 52701,'Explicit opening stock input required.',1;
IF (SELECT COUNT_BIG(*) FROM #OpeningBatch)<>1 OR NOT EXISTS(SELECT 1 FROM #OpeningLines)
    THROW 52701,'Exactly one approved vendor, actor and nonempty stock count required.',1;
DECLARE @VendorId int,@ActorId int,@Reference nvarchar(200);
SELECT @VendorId=VendorId,@ActorId=ActorId,@Reference=LTRIM(RTRIM(CountReference)) FROM #OpeningBatch;
IF @VendorId IS NULL OR @ActorId IS NULL OR @Reference IS NULL OR LEN(@Reference)=0
    THROW 52701,'Vendor, approver and physical-count reference required.',1;
IF EXISTS(SELECT 1 FROM #OpeningLines WHERE VendorListingId IS NULL OR Quantity IS NULL OR Quantity<=0 OR Quantity>1000000000)
    OR EXISTS(SELECT VendorListingId FROM #OpeningLines GROUP BY VendorListingId HAVING COUNT_BIG(*)>1)
    THROW 52702,'Invalid quantity or duplicate listing. Zero-count products remain without stock.',1;

BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS(SELECT 1 FROM dbo.Users u WITH(HOLDLOCK) JOIN dbo.[Role] r WITH(HOLDLOCK) ON r.RoleId=u.RoleId
        WHERE u.UserId=@ActorId AND u.IsActive=1 AND r.IsActive=1 AND r.RoleName=N'Admin')
        THROW 52703,'Active admin approval required.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.Vendors WITH(UPDLOCK,HOLDLOCK) WHERE VendorId=@VendorId AND Status='Approved')
        THROW 52704,'Approved vendor required.',1;
    IF EXISTS(SELECT 1 FROM #OpeningLines x LEFT JOIN dbo.VendorListings l WITH(UPDLOCK,HOLDLOCK)
        ON l.VendorListingId=x.VendorListingId AND l.VendorId=@VendorId
        LEFT JOIN dbo.Products p WITH(HOLDLOCK) ON p.ProductId=l.ProductId
        WHERE l.VendorListingId IS NULL OR p.ProductId IS NULL OR p.IsActive<>1)
        THROW 52705,'Missing, inactive or cross-vendor listing.',1;
    -- The original movement, not the current balance, is the immutable retry evidence.
    IF EXISTS(SELECT 1 FROM #OpeningLines x JOIN dbo.InventoryTransactions t WITH(UPDLOCK,HOLDLOCK)
        ON t.VendorListingId=x.VendorListingId AND t.SourceType='OpeningStock'
        WHERE t.SourceId<>@VendorId OR t.SourceLineId<>x.VendorListingId OR t.OnHandDelta<>x.Quantity
            OR t.ReservedDelta<>0 OR t.CreatedBy IS NULL OR t.CreatedBy<>@ActorId OR t.Reason IS NULL
            OR CONVERT(varbinary(400),t.Reason)<>CONVERT(varbinary(400),@Reference))
        THROW 52706,'Opening movement already exists with different evidence.',1;
    IF EXISTS(SELECT 1 FROM #OpeningLines x JOIN dbo.InventoryBalances b WITH(UPDLOCK,HOLDLOCK)
        ON b.VendorListingId=x.VendorListingId
        WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryTransactions t WITH(HOLDLOCK)
            WHERE t.VendorListingId=x.VendorListingId AND t.SourceType='OpeningStock'
                AND t.SourceId=@VendorId AND t.SourceLineId=x.VendorListingId))
        THROW 52707,'Existing balance requires reconciliation, not a second opening.',1;
    -- New opening stock may only be posted to Pending listings; never activate selling here.
    IF EXISTS(SELECT 1 FROM #OpeningLines x JOIN dbo.VendorListings l ON l.VendorListingId=x.VendorListingId
        WHERE l.Status<>'Pending' AND NOT EXISTS(SELECT 1 FROM dbo.InventoryBalances b WHERE b.VendorListingId=x.VendorListingId))
        THROW 52708,'New opening stock requires a pending listing.',1;

    DECLARE @New TABLE(VendorListingId int PRIMARY KEY, Quantity bigint NOT NULL);
    INSERT @New SELECT x.VendorListingId,x.Quantity FROM #OpeningLines x
        WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryBalances b WITH(UPDLOCK,HOLDLOCK) WHERE b.VendorListingId=x.VendorListingId);
    INSERT dbo.InventoryBalances(VendorListingId,OnHandQuantity,ReservedQuantity)
        SELECT VendorListingId,Quantity,0 FROM @New;
    INSERT dbo.InventoryTransactions(VendorListingId,OnHandDelta,ReservedDelta,SourceType,SourceId,SourceLineId,CreatedBy,Reason)
        SELECT VendorListingId,Quantity,0,'OpeningStock',@VendorId,VendorListingId,@ActorId,@Reference FROM @New;
    DECLARE @Posted int=@@ROWCOUNT;
    COMMIT TRANSACTION;
    SELECT @VendorId AS VendorId,@Posted AS PostedOpeningLines,
        (SELECT COUNT_BIG(*) FROM #OpeningLines)-@Posted AS ExistingExactLines;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
