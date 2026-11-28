:ON ERROR EXIT
IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 52510,'Disposable database required.',1;
GO
CREATE TABLE #AdminPageMapping(PageId int NOT NULL,ExpectedPageName nvarchar(100) NOT NULL,PageCode nvarchar(80) NOT NULL);
DECLARE @page int=(SELECT PageId FROM dbo.PageMaster WHERE PageNameEng=N'Legacy admin page');
INSERT #AdminPageMapping VALUES(@page,N'Legacy admin page',N'admin.brands');
INSERT dbo.[Role](RoleName,RoleNameArb,IsActive,CreatedAt,UpdatedAt)
VALUES(N'FixtureAdmin',N'اختبار',1,SYSUTCDATETIME(),SYSUTCDATETIME());
INSERT dbo.RolePermission(RoleId,PageId,ListPage,UpdatePage)
VALUES(SCOPE_IDENTITY(),@page,1,1);
GO
:r 15-map-admin-pages.sql
GO
:r 15-map-admin-pages.sql
GO
IF NOT EXISTS(SELECT 1 FROM dbo.PageMaster p JOIN dbo.RolePermission rp ON rp.PageId=p.PageId
    JOIN dbo.[Role] r ON r.RoleId=rp.RoleId
    WHERE p.PageNameEng=N'Legacy admin page' AND p.PageCode=N'admin.brands' AND p.PortalScope=N'Admin'
      AND r.RoleName=N'FixtureAdmin' AND rp.ListPage=1 AND rp.UpdatePage=1)
    THROW 52511,'Mapping failed to preserve the legacy grant.',1;
IF (SELECT COUNT(*) FROM dbo.PageMaster WHERE PortalScope=N'Vendor')<>5
    THROW 52512,'Vendor pages changed.',1;
DELETE rp FROM dbo.RolePermission rp JOIN dbo.[Role] r ON r.RoleId=rp.RoleId WHERE r.RoleName=N'FixtureAdmin';
DELETE dbo.[Role] WHERE RoleName=N'FixtureAdmin';
DROP TABLE #AdminPageMapping;
PRINT 'Reviewed admin page mapping and repeated migration checks passed.';
GO
