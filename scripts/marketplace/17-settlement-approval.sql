SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO
-- A test approval is bound to one immutable ledger revision. No bank/gateway payout.
IF OBJECT_ID(N'dbo.VendorSettlementApprovals',N'U') IS NULL
CREATE TABLE dbo.VendorSettlementApprovals(
    ApprovalId bigint IDENTITY PRIMARY KEY,
    VendorOrderId int NOT NULL REFERENCES dbo.VendorOrders(VendorOrderId),
    LedgerVersion bigint NOT NULL REFERENCES dbo.VendorSettlementLedger(EntryId),
    Amount decimal(18,3) NOT NULL CHECK(Amount>0),
    ApprovedBy int NOT NULL REFERENCES dbo.Users(UserId),
    ApprovedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    TestPayoutReference uniqueidentifier NULL,
    TestPayoutBy int NULL REFERENCES dbo.Users(UserId),
    TestPayoutAtUtc datetime2(3) NULL,
    CONSTRAINT UQ_SettlementApproval_Version UNIQUE(VendorOrderId,LedgerVersion),
    CONSTRAINT CK_SettlementApproval_TestPayout CHECK(
        (TestPayoutReference IS NULL AND TestPayoutBy IS NULL AND TestPayoutAtUtc IS NULL)
        OR (TestPayoutReference IS NOT NULL AND TestPayoutBy IS NOT NULL AND TestPayoutAtUtc IS NOT NULL))
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.VendorSettlementApprovals') AND name='UX_SettlementApproval_TestPayout')
    CREATE UNIQUE INDEX UX_SettlementApproval_TestPayout ON dbo.VendorSettlementApprovals(TestPayoutReference) WHERE TestPayoutReference IS NOT NULL;
GO
CREATE OR ALTER VIEW dbo.MarketplaceSettlementApprovalReview
AS
SELECT s.*, revision.LedgerVersion,
    CONVERT(decimal(18,3),COALESCE(paid.Amount,0)) AS TestPaidAmount,
    CONVERT(decimal(18,3),s.LedgerNet-CONVERT(decimal(18,3),COALESCE(paid.Amount,0))) AS TestOutstanding,
    a.ApprovalId, a.ApprovedAtUtc,a.TestPayoutReference,
    lastPaid.TestPayoutReference AS LastTestPayoutReference,lastPaid.TestPayoutAtUtc AS LastTestPayoutAtUtc,
    CONVERT(bit,CASE WHEN s.SettlementEligible=1 AND s.LedgerCurrent=1
        AND s.LedgerNet>COALESCE(paid.Amount,0) AND a.ApprovalId IS NOT NULL
        AND a.TestPayoutReference IS NULL AND a.Amount=s.LedgerNet-CONVERT(decimal(18,3),COALESCE(paid.Amount,0))
        THEN 1 ELSE 0 END) AS CanRecordTestPayout
FROM dbo.MarketplaceSettlementReview s
OUTER APPLY(SELECT MAX(EntryId) LedgerVersion FROM dbo.VendorSettlementLedger e WHERE e.VendorOrderId=s.VendorOrderId) revision
OUTER APPLY(SELECT SUM(Amount) Amount FROM dbo.VendorSettlementApprovals p
    WHERE p.VendorOrderId=s.VendorOrderId AND p.TestPayoutReference IS NOT NULL) paid
OUTER APPLY(SELECT TOP(1) p.TestPayoutReference,p.TestPayoutAtUtc FROM dbo.VendorSettlementApprovals p
    WHERE p.VendorOrderId=s.VendorOrderId AND p.TestPayoutReference IS NOT NULL ORDER BY p.ApprovalId DESC) lastPaid
LEFT JOIN dbo.VendorSettlementApprovals a ON a.VendorOrderId=s.VendorOrderId AND a.LedgerVersion=revision.LedgerVersion;
GO
-- Same parent-order/refund lock order as ledger sync; never trust posted amounts.
CREATE OR ALTER PROCEDURE dbo.ReviewVendorSettlementPreview
    @VendorOrderId int,@ActorId int,@LedgerVersion bigint,@Action varchar(12),@ApprovalId bigint=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @LedgerVersion IS NULL OR @LedgerVersion<=0 OR @Action IS NULL OR @Action NOT IN ('Approve','TestPayout')
        OR (@Action='TestPayout' AND (@ApprovalId IS NULL OR @ApprovalId<=0))
        THROW 52800,'Invalid settlement operation.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Users u WITH(HOLDLOCK) JOIN dbo.[Role] r WITH(HOLDLOCK) ON r.RoleId=u.RoleId
            WHERE u.UserId=@ActorId AND u.IsActive=1 AND r.IsActive=1 AND r.RoleName='Admin')
            THROW 52801,'Admin required.',1;
        DECLARE @order int=(SELECT OrderId FROM dbo.VendorOrders WHERE VendorOrderId=@VendorOrderId);
        IF NOT EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK)
            WHERE OrderId=@order AND PaymentStatus=1 AND MarketplaceRequestId IS NOT NULL)
            THROW 52802,'Paid order required.',1;
        DECLARE @refundCount bigint;
        SELECT @refundCount=COUNT_BIG(*) FROM dbo.MarketplaceRefundAttempts WITH(UPDLOCK,HOLDLOCK) WHERE OrderId=@order;
        DECLARE @current bigint,@outstanding decimal(18,3),@existing bigint,@canPay bit;
        SELECT @current=LedgerVersion,@outstanding=TestOutstanding,@existing=ApprovalId,@canPay=CanRecordTestPayout
        FROM dbo.MarketplaceSettlementApprovalReview
        WHERE VendorOrderId=@VendorOrderId AND SettlementEligible=1 AND LedgerCurrent=1;
        IF @current IS NULL OR @current<>@LedgerVersion
            THROW 52803,'Ledger revision or eligibility changed.',1;
        -- A repeated successful request is a no-op, never a second payout.
        IF @Action='Approve' AND @existing IS NOT NULL
        BEGIN
            COMMIT;
            RETURN;
        END;
        IF @Action='TestPayout' AND EXISTS(SELECT 1 FROM dbo.VendorSettlementApprovals WITH(UPDLOCK,HOLDLOCK)
            WHERE ApprovalId=@ApprovalId AND VendorOrderId=@VendorOrderId AND LedgerVersion=@LedgerVersion
                AND TestPayoutReference IS NOT NULL)
        BEGIN
            COMMIT;
            RETURN;
        END;
        IF @outstanding IS NULL OR @outstanding<=0 THROW 52804,'No positive test outstanding.',1;
        IF @Action='Approve'
            INSERT dbo.VendorSettlementApprovals(VendorOrderId,LedgerVersion,Amount,ApprovedBy)
            VALUES(@VendorOrderId,@LedgerVersion,@outstanding,@ActorId);
        ELSE
        BEGIN
            IF @existing IS NULL OR @existing<>@ApprovalId OR @canPay<>1
                THROW 52805,'Current approval required.',1;
            UPDATE dbo.VendorSettlementApprovals
            SET TestPayoutReference=NEWID(),TestPayoutBy=@ActorId,TestPayoutAtUtc=SYSUTCDATETIME()
            WHERE ApprovalId=@ApprovalId AND VendorOrderId=@VendorOrderId AND LedgerVersion=@LedgerVersion
                AND Amount=@outstanding AND TestPayoutReference IS NULL;
            IF @@ROWCOUNT<>1 THROW 52805,'Approval changed.',1;
        END;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
