-- Read-only prerequisite for staff authentication upgrade. Never returns password values.
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL THROW 51201, 'Users table missing.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users')
 AND name = N'PasswordHash' AND
 ((TYPE_NAME(system_type_id) = N'nvarchar' AND (max_length = -1 OR max_length >= 512))
 OR (TYPE_NAME(system_type_id) = N'varchar' AND (max_length = -1 OR max_length >= 256))))
 THROW 51202, 'Review and widen Users.PasswordHash to at least 256 characters before deployment.', 1;
SELECT COUNT_BIG(*) AS StaffAccounts,
 COUNT_BIG(CASE WHEN PasswordHash LIKE N'staff-v1:%' THEN 1 END) AS MarkedHashes,
 COUNT_BIG(CASE WHEN PasswordHash IS NULL OR PasswordHash = N'' THEN 1 END) AS MissingCredentials,
 COUNT_BIG(CASE WHEN PasswordHash IS NOT NULL AND PasswordHash NOT LIKE N'staff-v1:%' THEN 1 END) AS LegacyCredentials
FROM dbo.Users;
SELECT COUNT_BIG(*) AS DuplicateEmailGroups FROM
 (SELECT Email FROM dbo.Users GROUP BY Email HAVING COUNT_BIG(*) > 1) d;
