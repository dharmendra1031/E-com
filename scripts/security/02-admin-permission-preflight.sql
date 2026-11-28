-- Read-only inventory. Run against a restored copy of the deployment database.
-- No customer/staff personal data, passwords or changes are returned.
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.PageMaster', N'U') IS NULL OR OBJECT_ID(N'dbo.RolePermission', N'U') IS NULL
    OR OBJECT_ID(N'dbo.Role', N'U') IS NULL OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
    THROW 52200, 'Existing security schema not found.', 1;

SELECT DB_NAME() AS DatabaseName;
SELECT t.name AS TableName,c.name AS ColumnName,TYPE_NAME(c.user_type_id) AS DataType,
    c.max_length,c.is_nullable,c.is_identity
FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
WHERE t.schema_id=SCHEMA_ID(N'dbo') AND t.name IN (N'PageMaster',N'RolePermission',N'Role',N'Users')
ORDER BY t.name,c.column_id;

SELECT r.RoleId,r.RoleName,r.IsActive,
    (SELECT COUNT(*) FROM dbo.Users u WHERE u.RoleId=r.RoleId AND u.IsActive=1) AS ActiveStaffCount,
    (SELECT COUNT(*) FROM dbo.RolePermission rp WHERE rp.RoleId=r.RoleId) AS PermissionCount
FROM dbo.[Role] r ORDER BY r.RoleId;

SELECT p.PageId,p.PageNameEng,p.PageNameArb,
    (SELECT COUNT(*) FROM dbo.RolePermission rp WHERE rp.PageId=p.PageId) AS GrantCount
FROM dbo.PageMaster p ORDER BY p.PageId;

SELECT rp.RoleId,r.RoleName,rp.PageId,p.PageNameEng,rp.ListPage,rp.CreatePage,
    rp.UpdatePage,rp.DeletePage,rp.ReportView,rp.ReportPrint
FROM dbo.RolePermission rp LEFT JOIN dbo.[Role] r ON r.RoleId=rp.RoleId
LEFT JOIN dbo.PageMaster p ON p.PageId=rp.PageId
ORDER BY rp.RoleId,rp.PageId;

SELECT RoleId,PageId,COUNT(*) AS DuplicateCount FROM dbo.RolePermission
GROUP BY RoleId,PageId HAVING COUNT(*)>1;
SELECT rp.RoleId,rp.PageId FROM dbo.RolePermission rp
LEFT JOIN dbo.[Role] r ON r.RoleId=rp.RoleId LEFT JOIN dbo.PageMaster p ON p.PageId=rp.PageId
WHERE r.RoleId IS NULL OR p.PageId IS NULL;

IF COL_LENGTH(N'dbo.PageMaster',N'PageCode') IS NOT NULL
   AND COL_LENGTH(N'dbo.PageMaster',N'PortalScope') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'SELECT PageId,PageNameEng,PageCode,PortalScope FROM dbo.PageMaster ORDER BY PageId;
        SELECT rp.RoleId,rp.PageId FROM dbo.RolePermission rp JOIN dbo.PageMaster p ON p.PageId=rp.PageId
        JOIN dbo.[Role] r ON r.RoleId=rp.RoleId WHERE p.PortalScope <>
        CASE WHEN r.RoleName IN (N''VendorOwner'',N''VendorManager'',N''VendorStaff'') THEN N''Vendor'' ELSE N''Admin'' END;';
END;
