-- Review preflight output and supply a session-local mapping before running this script.
-- No page or permission rows are created or deleted. Existing grants retain their PageId.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'tempdb..#AdminPageMapping') IS NULL
    THROW 52500,'Provide reviewed #AdminPageMapping in the same SQL session.',1;
IF NOT EXISTS(SELECT 1 FROM #AdminPageMapping)
    THROW 52500,'Reviewed mapping is empty.',1;
DECLARE @supported TABLE(Code nvarchar(80) PRIMARY KEY);
INSERT @supported VALUES
(N'admin.dashboard'),(N'admin.orders'),(N'admin.customers'),(N'admin.stock'),
(N'admin.products'),(N'admin.categories'),(N'admin.subcategories'),(N'admin.topcategories'),
(N'admin.brands'),(N'admin.deals'),(N'admin.coupons'),(N'admin.banners'),(N'admin.notifications'),
(N'admin.blog'),(N'admin.reports'),(N'admin.company'),(N'admin.areas'),(N'admin.blocks'),
(N'admin.contact'),(N'admin.about'),(N'admin.theme');
BEGIN TRY
    BEGIN TRANSACTION;
    IF EXISTS(SELECT 1 FROM #AdminPageMapping m LEFT JOIN @supported s
        ON m.PageCode COLLATE Latin1_General_100_BIN2=s.Code COLLATE Latin1_General_100_BIN2
        WHERE s.Code IS NULL)
       OR EXISTS(SELECT PageId FROM #AdminPageMapping GROUP BY PageId HAVING COUNT(*)<>1)
       OR EXISTS(SELECT PageCode FROM #AdminPageMapping GROUP BY PageCode HAVING COUNT(*)<>1)
        THROW 52501,'Invalid or duplicate admin page mapping.',1;
    IF EXISTS(SELECT 1 FROM #AdminPageMapping m
        LEFT JOIN dbo.PageMaster p WITH(UPDLOCK,HOLDLOCK) ON p.PageId=m.PageId
        WHERE p.PageId IS NULL OR p.PortalScope<>N'Admin'
          OR p.PageNameEng IS NULL OR m.ExpectedPageName IS NULL
          OR p.PageNameEng COLLATE Latin1_General_100_BIN2<>m.ExpectedPageName COLLATE Latin1_General_100_BIN2
          OR (p.PageCode IS NOT NULL AND p.PageCode COLLATE Latin1_General_100_BIN2<>m.PageCode COLLATE Latin1_General_100_BIN2))
        THROW 52502,'Existing page or scope differs from reviewed mapping.',1;
    IF EXISTS(SELECT 1 FROM #AdminPageMapping m JOIN dbo.PageMaster p WITH(UPDLOCK,HOLDLOCK)
        ON p.PageCode=m.PageCode AND p.PageId<>m.PageId)
        THROW 52503,'Page code is already assigned to another page.',1;
    UPDATE p SET PageCode=m.PageCode FROM dbo.PageMaster p JOIN #AdminPageMapping m ON m.PageId=p.PageId
    WHERE p.PageCode IS NULL;
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
GO
