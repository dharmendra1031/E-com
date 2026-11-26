-- Explicit role seed; no accounts or permissions granted. Restored backup first.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.Role', N'U') IS NULL
    THROW 51120, 'Role table missing.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @Roles TABLE (RoleName nvarchar(100), RoleNameArb nvarchar(100));
    INSERT @Roles VALUES (N'VendorOwner', N'مالك البائع'), (N'VendorManager', N'مدير البائع'), (N'VendorStaff', N'موظف البائع');
    IF EXISTS (SELECT r.RoleName FROM dbo.[Role] r WITH (UPDLOCK, HOLDLOCK)
        JOIN @Roles v ON v.RoleName = r.RoleName GROUP BY r.RoleName HAVING COUNT(*) > 1)
        THROW 51121, 'Duplicate vendor role names; resolve manually.', 1;
    INSERT dbo.[Role] (RoleName, RoleNameArb, IsActive, CreatedAt, UpdatedAt)
    SELECT v.RoleName, v.RoleNameArb, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
    FROM @Roles v
    WHERE NOT EXISTS (SELECT 1 FROM dbo.[Role] r WITH (UPDLOCK, HOLDLOCK) WHERE r.RoleName = v.RoleName);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
