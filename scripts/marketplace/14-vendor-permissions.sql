-- Run on a restored backup first, before deploying the new application and procedures.
-- No roles or permissions are automatically granted. Existing admin page IDs and rights remain unchanged.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.PageMaster',N'U') IS NULL OR OBJECT_ID(N'dbo.RolePermission',N'U') IS NULL
    THROW 52100,'Existing permission schema required.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS(SELECT RoleId,PageId FROM dbo.RolePermission WITH(UPDLOCK,HOLDLOCK) GROUP BY RoleId,PageId HAVING COUNT(*)>1)
        THROW 52101,'Duplicate role/page permissions require manual review.',1;
    IF EXISTS(SELECT 1 FROM dbo.RolePermission rp LEFT JOIN dbo.PageMaster p ON p.PageId=rp.PageId
        LEFT JOIN dbo.[Role] r ON r.RoleId=rp.RoleId WHERE p.PageId IS NULL OR r.RoleId IS NULL)
        THROW 52102,'Orphan permissions require manual review.',1;
    IF COL_LENGTH(N'dbo.PageMaster',N'PageCode') IS NULL
        ALTER TABLE dbo.PageMaster ADD PageCode nvarchar(80) NULL;
    IF COL_LENGTH(N'dbo.PageMaster',N'PortalScope') IS NULL
        ALTER TABLE dbo.PageMaster ADD PortalScope nvarchar(10) NOT NULL CONSTRAINT DF_PageMaster_PortalScope DEFAULT N'Admin';
    EXEC sys.sp_executesql N'
        IF EXISTS(SELECT 1 FROM dbo.PageMaster WHERE PortalScope NOT IN (N''Admin'',N''Vendor'') OR PortalScope IS NULL)
            THROW 52103,''Unknown portal scope.'',1;
        IF EXISTS(SELECT 1 FROM dbo.RolePermission rp JOIN dbo.PageMaster p ON p.PageId=rp.PageId
            JOIN dbo.[Role] r ON r.RoleId=rp.RoleId
            WHERE p.PortalScope <> CASE WHEN r.RoleName IN (N''VendorOwner'',N''VendorManager'',N''VendorStaff'') THEN N''Vendor'' ELSE N''Admin'' END)
            THROW 52104,''Cross-portal permission requires manual review.'',1;
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N''dbo.PageMaster'') AND name=N''UX_PageMaster_PageCode'')
            CREATE UNIQUE INDEX UX_PageMaster_PageCode ON dbo.PageMaster(PageCode) WHERE PageCode IS NOT NULL;
        IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N''dbo.RolePermission'') AND name=N''UX_RolePermission_Role_Page'')
            CREATE UNIQUE INDEX UX_RolePermission_Role_Page ON dbo.RolePermission(RoleId,PageId);
        DECLARE @pages TABLE(Code nvarchar(80),En nvarchar(100),Ar nvarchar(100));
        INSERT @pages VALUES
          (N''vendor.dashboard'',N''Vendor Dashboard'',N''لوحة تحكم البائع''),
          (N''vendor.listings'',N''Vendor Listings'',N''عروض البائع''),
          (N''vendor.stock'',N''Vendor Stock'',N''مخزون البائع''),
          (N''vendor.orders'',N''Vendor Orders'',N''طلبات البائع''),
          (N''vendor.finance'',N''Vendor Finance'',N''مالية البائع'');
        IF EXISTS(SELECT 1 FROM dbo.PageMaster p JOIN @pages s ON s.Code=p.PageCode WHERE p.PortalScope<>N''Vendor'')
            THROW 52105,''Vendor page code conflict.'',1;
        INSERT dbo.PageMaster(PageCode,PortalScope,PageNameEng,PageNameArb,CreatedAt)
        SELECT Code,N''Vendor'',En,Ar,SYSUTCDATETIME() FROM @pages s
        WHERE NOT EXISTS(SELECT 1 FROM dbo.PageMaster p WITH(UPDLOCK,HOLDLOCK) WHERE p.PageCode=s.Code);';
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
GO
