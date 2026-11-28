SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
-- Single transaction for manual movements. Never called by the legacy stock writers.
CREATE OR ALTER PROCEDURE dbo.PostInventoryAdjustment
    @VendorId int,
    @VendorListingId int,
    @UserId int,
    @RequestId uniqueidentifier,
    @Delta bigint,
    @Reason nvarchar(200),
    @AdminPortal bit = 0
AS
BEGIN
    SET ANSI_PADDING ON;
    SET ANSI_WARNINGS ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Reason = LTRIM(RTRIM(@Reason));
    IF @RequestId IS NULL OR @RequestId = '00000000-0000-0000-0000-000000000000'
       OR @Delta IS NULL OR @Delta = 0 OR @Delta < -1000000000 OR @Delta > 1000000000
       OR @Reason IS NULL OR LEN(@Reason) = 0
        THROW 51310, 'Invalid stock adjustment.', 1;
    IF @@TRANCOUNT <> 0 THROW 51311, 'Manual adjustment requires its own transaction.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        -- Recheck authorization in the same transaction; hold membership/role/vendor locks until commit.
        IF @AdminPortal IS NULL THROW 51312,'Portal denied.',1;
        IF @AdminPortal=1
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.Users u WITH(HOLDLOCK)
                JOIN dbo.[Role] r WITH(HOLDLOCK) ON r.RoleId=u.RoleId
                WHERE u.UserId=@UserId AND u.IsActive=1 AND r.IsActive=1
                AND (r.RoleName=N'Admin' OR (r.RoleName IN(N'AdminManager',N'AdminStaff')
                    AND EXISTS(SELECT 1 FROM dbo.RolePermission rp WITH(HOLDLOCK)
                        JOIN dbo.PageMaster pm WITH(HOLDLOCK) ON pm.PageId=rp.PageId
                        WHERE rp.RoleId=u.RoleId AND pm.PortalScope='Admin' AND pm.PageCode='admin.stock'
                          AND rp.ListPage=1 AND rp.UpdatePage=1))))
                THROW 51312,'Admin inventory permission denied.',1;
        END
        ELSE IF NOT EXISTS (
            SELECT 1 FROM dbo.VendorUsers m WITH (HOLDLOCK)
            JOIN dbo.Users u WITH (HOLDLOCK) ON u.UserId = m.UserId
            JOIN dbo.[Role] r WITH (HOLDLOCK) ON r.RoleId = u.RoleId
            JOIN dbo.Vendors v WITH (HOLDLOCK) ON v.VendorId = m.VendorId
            WHERE m.VendorId = @VendorId AND m.UserId = @UserId AND m.IsActive = 1
              AND u.IsActive = 1 AND r.IsActive = 1 AND r.RoleName IN (N'VendorOwner', N'VendorManager', N'VendorStaff')
              AND v.Status = 'Approved'
              AND EXISTS (SELECT 1 FROM dbo.RolePermission rp WITH(HOLDLOCK) JOIN dbo.PageMaster pm WITH(HOLDLOCK) ON pm.PageId=rp.PageId
                  WHERE rp.RoleId=u.RoleId AND pm.PortalScope='Vendor' AND pm.PageCode='vendor.stock' AND rp.ListPage=1 AND rp.UpdatePage=1)
              AND EXISTS (SELECT 1 FROM dbo.RolePermission rp WITH(HOLDLOCK) JOIN dbo.PageMaster pm WITH(HOLDLOCK) ON pm.PageId=rp.PageId
                  WHERE rp.RoleId=u.RoleId AND pm.PortalScope='Vendor' AND pm.PageCode='vendor.listings' AND rp.ListPage=1))
            THROW 51312, 'Vendor membership or permission denied.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.VendorListings WITH (UPDLOCK, HOLDLOCK)
                       WHERE VendorListingId = @VendorListingId AND VendorId = @VendorId)
            THROW 51312, 'Listing unavailable.', 1;

        IF EXISTS (SELECT 1 FROM dbo.InventoryTransactions WITH (UPDLOCK, HOLDLOCK) WHERE RequestId = @RequestId)
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.InventoryTransactions WHERE RequestId = @RequestId
                AND VendorListingId = @VendorListingId AND CreatedBy = @UserId
                AND OnHandDelta = @Delta AND ReservedDelta = 0 AND SourceType = 'ManualStock'
                AND CONVERT(varbinary(400), Reason) = CONVERT(varbinary(400), @Reason))
                THROW 51313, 'Request already used with different details.', 1;
            COMMIT TRANSACTION;
            RETURN;
        END;

        IF NOT EXISTS (SELECT 1 FROM dbo.InventoryBalances WITH (UPDLOCK, HOLDLOCK) WHERE VendorListingId = @VendorListingId)
        BEGIN
            IF @Delta < 0 THROW 51314, 'Insufficient available stock.', 1;
            INSERT dbo.InventoryBalances (VendorListingId, OnHandQuantity) VALUES (@VendorListingId, 0);
        END;
        UPDATE dbo.InventoryBalances
           SET OnHandQuantity = OnHandQuantity + @Delta, UpdatedAtUtc = SYSUTCDATETIME()
         WHERE VendorListingId = @VendorListingId
           AND AvailableQuantity >= CASE WHEN @Delta < 0 THEN -@Delta ELSE 0 END
           AND (@Delta <= 0 OR OnHandQuantity <= 9223372036854775807 - CASE WHEN @Delta > 0 THEN @Delta ELSE 0 END);
        IF @@ROWCOUNT <> 1 THROW 51314, 'Insufficient available stock or quantity limit.', 1;

        DECLARE @key binary(16) = CONVERT(binary(16), @RequestId);
        INSERT dbo.InventoryTransactions (VendorListingId, OnHandDelta, ReservedDelta, SourceType, SourceId, SourceLineId, CreatedBy, RequestId, Reason)
        VALUES (@VendorListingId, @Delta, 0, 'ManualStock', CONVERT(bigint, SUBSTRING(@key, 1, 8)),
            CONVERT(bigint, SUBSTRING(@key, 9, 8)), @UserId, @RequestId, @Reason);
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
