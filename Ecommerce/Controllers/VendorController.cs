using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Data;
using EcommerceWebApplication.Helper;
using MF.Models;

namespace EcommerceWebApplication.Controllers;

[Authorize(Policy = "SuperAdminOnly")]
[AutoValidateAntiforgeryToken]
public class VendorController(EcommerceDbContext db, IConfiguration configuration) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (configuration["Marketplace:VendorMasterEnabled"] != "true")
        {
            context.Result = NotFound();
            return;
        }
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (!int.TryParse(User.FindFirstValue("UserId"), out var userId) ||
            !await db.Users.AnyAsync(u => u.UserId == userId && u.IsActive &&
                u.Email == email && u.Role != null && u.Role.IsActive && u.Role.RoleName == "Admin",
                context.HttpContext.RequestAborted))
        {
            context.Result = Forbid();
            return;
        }
        await next();
    }

    [HttpGet]
    public async Task<IActionResult> Commission(int id,CancellationToken ct)
    {
        if (!configuration.GetValue("Marketplace:FinancePreviewEnabled",false)) return NotFound();
        var form=await db.Database.SqlQuery<VendorCommissionForm>($"""
            SELECT v.VendorId,v.Name AS VendorName,COALESCE(p.PolicyId,0) AS ExpectedPolicyId,p.RatePercent
            FROM dbo.Vendors v OUTER APPLY(SELECT TOP(1) PolicyId,RatePercent FROM dbo.VendorCommissionPolicies
                WHERE VendorId=v.VendorId ORDER BY PolicyId DESC) p WHERE v.VendorId={id}
            """).SingleOrDefaultAsync(ct);
        return form==null?NotFound():View(form);
    }

    [HttpPost]
    public async Task<IActionResult> Commission(VendorCommissionForm form,CancellationToken ct)
    {
        if (!configuration.GetValue("Marketplace:FinancePreviewEnabled",false)) return NotFound();
        if (!ModelState.IsValid || form.RatePercent is not decimal rate || decimal.Round(rate,4)!=rate ||
            form.VendorId<=0 || form.ExpectedPolicyId<0 || !int.TryParse(User.FindFirstValue("UserId"),out var actor)) return BadRequest();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                EXEC dbo.SetVendorCommission @VendorId={form.VendorId},@ActorId={actor},
                    @ExpectedPolicyId={form.ExpectedPolicyId},@RatePercent={rate}
                """,ct);
            TempData["FinanceMessage"]="Commission policy saved. / تم حفظ العمولة.";
        }
        catch(SqlException ex) when(ex.Number is >=52400 and <=52403)
        {
            TempData["FinanceMessage"]="Policy not changed. Reload and review. / لم تتغير العمولة.";
        }
        return RedirectToAction(nameof(Commission),new {id=form.VendorId});
    }

    [HttpGet]
    [ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
    public async Task<IActionResult> RefundReview([FromServices] IWebHostEnvironment environment,int page=1,CancellationToken ct=default)
    {
        if (!configuration.GetValue("Marketplace:FinancePreviewEnabled",false)) return NotFound();
        page=Math.Clamp(page,1,100000);
        var rows=await RefundRows().OrderByDescending(r=>r.ReturnId).Skip((page-1)*25).Take(26).ToListAsync(ct);
        ViewBag.Page=page;ViewBag.HasNext=rows.Count>25;
        ViewBag.CanRefund=MarketplaceRefundGateway.Enabled(configuration,environment);
        return View(rows.Take(25).ToList());
    }

    private IQueryable<MarketplaceRefundReviewRow> RefundRows() => db.Database.SqlQuery<MarketplaceRefundReviewRow>($"""
            SELECT r.ReturnId,r.OrderItemId,r.OrderId,r.VendorId,r.VendorName,r.ProductName,r.Quantity,r.OriginalUnitPrice,
                r.ReturnedMerchandiseValue,r.InvoiceId,r.PaymentId,r.ReceivedAtUtc,r.ReturnReceiptId,
                a.MerchantReference AS RefundReference,a.Amount AS RefundAmount,a.GatewayRefundId
            FROM dbo.MarketplaceRefundReview r LEFT JOIN dbo.MarketplaceRefundAttempts a ON a.ReturnId=r.ReturnId
            """);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MarketplacePayment")]
    public async Task<IActionResult> StartSandboxRefund(int returnId,decimal amount,bool confirmed,
        [FromServices] IWebHostEnvironment environment,[FromServices] IMFClient gateway,CancellationToken ct)
    {
        if(!MarketplaceRefundGateway.Enabled(configuration,environment)) return NotFound();
        if(!ModelState.IsValid || !confirmed || amount<=0 || decimal.Round(amount,3)!=amount ||
            !int.TryParse(User.FindFirstValue("UserId"),out var actor)) return BadRequest();
        var row=await RefundRows().SingleOrDefaultAsync(r=>r.ReturnId==returnId,ct);
        if(row==null) return NotFound();
        if(row.RefundReference.HasValue || amount>row.ReturnedMerchandiseValue) return Conflict();
        var reference=Guid.NewGuid();
        try
        {
            // Require a fresh invoice inquiry and no unrecognized prior refund before sending funds.
            var existing=await MarketplaceRefundGateway.InquiryAsync(gateway,row.InvoiceId);
            if(existing?.IsSuccess!=true || existing.Data?.RefundStatusResult==null) return Conflict("Refund inquiry unavailable.");
            var known=await db.Database.SqlQuery<long>($"""
                SELECT GatewayRefundId AS Value FROM dbo.MarketplaceRefundAttempts
                WHERE InvoiceId={row.InvoiceId} AND GatewayRefundId IS NOT NULL
                """).ToListAsync(ct);
            if(existing.Data.RefundStatusResult.Any(r=>r==null || r.InvoiceId!=row.InvoiceId || !known.Contains(r.RefundId)))
                return Conflict("Existing gateway refund requires review.");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                EXEC dbo.BeginMarketplaceRefund @ReturnId={returnId},@ActorId={actor},@Reference={reference},@Amount={amount}
                """,ct);
            ct.ThrowIfCancellationRequested();
            await MarketplaceRefundGateway.RequestAsync(gateway,row.InvoiceId,reference,amount);
            TempData["RefundMessage"]="Refund attempt recorded; verify gateway status. / تم تسجيل المحاولة، تحقق من الحالة.";
        }
        catch(Exception) when(!ct.IsCancellationRequested)
        {
            TempData["RefundMessage"]="Refund not confirmed. Review existing attempt; do not retry blindly. / الاسترداد غير مؤكد.";
        }
        return RedirectToAction(nameof(RefundReview));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MarketplacePayment")]
    public async Task<IActionResult> VerifySandboxRefund(int returnId,[FromServices] IWebHostEnvironment environment,
        [FromServices] IMFClient gateway,CancellationToken ct)
    {
        if(!MarketplaceRefundGateway.Enabled(configuration,environment)) return NotFound();
        if(!int.TryParse(User.FindFirstValue("UserId"),out var actor)) return Forbid();
        var row=await RefundRows().SingleOrDefaultAsync(r=>r.ReturnId==returnId,ct);
        if(row?.RefundReference is not Guid reference || row.RefundAmount is not decimal amount) return NotFound();
        try
        {
            var response=await MarketplaceRefundGateway.InquiryAsync(gateway,row.InvoiceId);
            var verified=MarketplaceRefundGateway.VerifiedRefundId(response,row.InvoiceId,reference,amount);
            if(verified is long refundId)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    EXEC dbo.ConfirmMarketplaceRefund @ReturnId={returnId},@ActorId={actor},@Reference={reference},
                        @InvoiceId={row.InvoiceId},@Amount={amount},@GatewayRefundId={refundId}
                    """,ct);
                TempData["RefundMessage"]="Sandbox refund verified. / تم التحقق من الاسترداد التجريبي.";
            }
            else TempData["RefundMessage"]="No conclusive refund evidence. / لا يوجد إثبات استرداد مؤكد.";
        }
        catch(Exception) when(!ct.IsCancellationRequested)
        {
            TempData["RefundMessage"]="Verification unavailable; no status changed. / التحقق غير متاح.";
        }
        return RedirectToAction(nameof(RefundReview));
    }

    [HttpGet]
    public async Task<IActionResult> FinanceReview([FromServices] IWebHostEnvironment environment,int page=1,CancellationToken ct=default)
    {
        if (!configuration.GetValue("Marketplace:FinancePreviewEnabled",false)) return NotFound();
        page=Math.Clamp(page,1,100000);
        var rows=await db.Database.SqlQuery<MarketplaceFinanceRow>($"""
            SELECT VendorOrderId,OrderId,VendorId,VendorName,MerchandiseSubtotal,CommissionRatePercent,
                CommissionAmount,MerchandiseAfterCommission,FulfillmentStatus,ReviewState,
                ReceivedReturnQuantity,ReturnedMerchandiseValue,RemainingMerchandiseValue,OpenReturnQuantity,
                VerifiedRefundValue,LedgerNet,LedgerCommissionReversal,SettlementEligible,LedgerCurrent FROM dbo.MarketplaceSettlementReview
            """).OrderByDescending(r=>r.VendorOrderId).Skip((page-1)*25).Take(26).ToListAsync(ct);
        ViewBag.Page=page;ViewBag.HasNext=rows.Count>25;
        ViewBag.CanPostSettlement=SettlementPreviewEnabled(environment);
        return View(rows.Take(25).ToList());
    }

    private bool SettlementPreviewEnabled(IWebHostEnvironment environment) =>
        MarketplaceRefundGateway.Enabled(configuration,environment) &&
        configuration.GetValue("Marketplace:SettlementPreviewEnabled",false);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostSettlementPreview(int vendorOrderId,int page,
        [FromServices] IWebHostEnvironment environment,CancellationToken ct)
    {
        if(!SettlementPreviewEnabled(environment)) return NotFound();
        if(!ModelState.IsValid || vendorOrderId<=0 || !int.TryParse(User.FindFirstValue("UserId"),out var actor))
            return BadRequest();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.PostVendorSettlementPreview @VendorOrderId={vendorOrderId},@ActorId={actor}",ct);
            TempData["SettlementMessage"]="Sandbox ledger synchronized. No payout issued. / تمت مطابقة السجل التجريبي.";
        }
        catch(SqlException ex) when(ex.Number is >=52701 and <=52704)
        {
            TempData["SettlementMessage"]="Ledger unchanged. Review eligibility and evidence. / لم يتغير السجل.";
        }
        return RedirectToAction(nameof(FinanceReview),new {page});
    }

    [HttpGet]
    public async Task<IActionResult> ReturnReview(int page=1,CancellationToken ct=default,string? status=null,int? orderId=null)
    {
        if (!configuration.GetValue("Marketplace:ReturnWorkflowEnabled",false)) return NotFound();
        page=Math.Clamp(page,1,100000);
        if (status is not ("Pending" or "Approved" or "Rejected" or "Received")) status=null;
        if(orderId is <=0) return BadRequest();
        var query=db.Database.SqlQuery<MarketplaceReturnRow>($"""
            SELECT ReturnId,OrderId,CustomerId,ProductName,ProductNameArb,Quantity,Reason,Status,RequestedAtUtc
            FROM dbo.MarketplaceReturnReview
            """);
        if(status!=null) query=query.Where(r=>r.Status==status);
        if(orderId.HasValue) query=query.Where(r=>r.OrderId==orderId.Value);
        var rows=await query.OrderByDescending(r=>r.ReturnId).Skip((page-1)*25).Take(26).ToListAsync(ct);
        ViewBag.Status=status;ViewBag.OrderId=orderId;
        ViewBag.Page=page;ViewBag.HasNext=rows.Count>25;
        return View(rows.Take(25).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> ReviewReturn(int returnId,string expectedStatus,string newStatus,CancellationToken ct,bool receiptConfirmed=false)
    {
        if (!configuration.GetValue("Marketplace:ReturnWorkflowEnabled",false)) return NotFound();
        if (!ModelState.IsValid || !int.TryParse(User.FindFirstValue("UserId"),out var actor) ||
            (newStatus=="Received" && !receiptConfirmed)) return BadRequest();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                EXEC dbo.ReviewMarketplaceReturn @ReturnId={returnId},@ActorId={actor},
                    @ExpectedStatus={expectedStatus},@NewStatus={newStatus}
                """,ct);
            TempData["ReturnMessage"]="Return status saved. / تم حفظ حالة الإرجاع.";
        }
        catch(SqlException ex) when(ex.Number is >=52300 and <=52306 or >=51900 and <=51904)
        {
            TempData["ReturnMessage"]="Return status unchanged. Review request. / لم تتغير الحالة.";
        }
        return RedirectToAction(nameof(ReturnReview));
    }

    [HttpGet]
    public async Task<IActionResult> PaymentReview([FromServices] IWebHostEnvironment environment, int page=1,CancellationToken ct=default)
    {
        if (!configuration.GetValue("Marketplace:PaymentReviewEnabled",false)) return NotFound();
        page=Math.Clamp(page,1,100000);
        var rows=await db.Database.SqlQuery<MarketplacePaymentReviewRow>($"""
            SELECT o.OrderId,o.CustomerId,o.TotalAmount,o.PaymentStatus,o.CreatedAt,p.InvoiceId,
                COALESCE(p.State,'Not started') AS AttemptState
            FROM dbo.Orders o LEFT JOIN dbo.MarketplacePaymentAttempts p ON p.OrderId=o.OrderId
            WHERE o.MarketplaceRequestId IS NOT NULL AND o.PaymentStatus=2
            """).OrderByDescending(o=>o.OrderId).Skip((page-1)*25).Take(26).ToListAsync(ct);
        ViewBag.Page=page;ViewBag.HasNext=rows.Count>25;
        ViewBag.CanReconcile=MarketplacePaymentReconciliation.SandboxEnabled(configuration, environment);
        return View(rows.Take(25).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MarketplacePayment")]
    public async Task<IActionResult> ReconcilePayment(int orderId, [FromServices] IWebHostEnvironment environment,
        [FromServices] IPaymentService gateway, CancellationToken ct)
    {
        if (!configuration.GetValue("Marketplace:PaymentReviewEnabled",false) ||
            !MarketplacePaymentReconciliation.SandboxEnabled(configuration, environment)) return NotFound();
        var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(o =>
            o.OrderId == orderId && o.MarketplaceRequestId != null && o.PaymentStatus == 2, ct);
        if (order?.MarketplaceRequestId is not Guid reference) return NotFound();
        try
        {
            TempData["PaymentReviewMessage"] = await MarketplacePaymentReconciliation.VerifyLinkedAsync(
                db, gateway, order.OrderId, reference, order.TotalAmount, ct)
                ? "Payment verified and posted. / تم التحقق من الدفع وتسجيله."
                : "No conclusive payment evidence. No changes made. / لا يوجد إثبات دفع مؤكد.";
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<VendorController>>()
                .LogWarning(ex, "Sandbox payment review failed for order {OrderId}.", orderId);
            TempData["PaymentReviewMessage"]="Payment requires review. Do not charge again. / يلزم مراجعة الدفع.";
        }
        return RedirectToAction(nameof(PaymentReview));
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken ct = default)
    {
        const int pageSize = 25;
        var query = db.Vendors.AsNoTracking();
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(v => v.VendorCode.Contains(search) || v.Name.Contains(search) ||
                (v.NameArb != null && v.NameArb.Contains(search)));
        var count = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(count / (double)pageSize)));
        ViewBag.Search = search;
        return View(new PagedResult<Vendor>
        {
            Items = await query.OrderBy(v => v.VendorId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct),
            PageNumber = page, PageSize = pageSize, TotalItems = count
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id = 0, CancellationToken ct = default)
    {
        if (id == 0) return View(new VendorForm());
        var vendor = await db.Vendors.AsNoTracking().SingleOrDefaultAsync(v => v.VendorId == id, ct);
        if (vendor == null) return NotFound();
        return View(new VendorForm { VendorId = id, VendorCode = vendor.VendorCode, Name = vendor.Name, NameArb = vendor.NameArb });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(VendorForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        var code = form.VendorCode.Trim();
        var name = form.Name.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("", "Vendor code and name are required. / رمز البائع والاسم مطلوبان.");
            return View(form);
        }
        var vendor = form.VendorId == 0 ? new Vendor { VendorCode = code, CreatedAtUtc = DateTime.UtcNow }
            : await db.Vendors.SingleOrDefaultAsync(v => v.VendorId == form.VendorId, ct);
        if (vendor == null) return NotFound();
        if (form.VendorId != 0 && vendor.VendorCode != code) return BadRequest();
        vendor.Name = name;
        vendor.NameArb = string.IsNullOrWhiteSpace(form.NameArb) ? null : form.NameArb.Trim();
        if (form.VendorId == 0) db.Vendors.Add(vendor);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError(nameof(form.VendorCode), "Vendor code already exists. / رمز البائع موجود بالفعل.");
            return View(form);
        }
        TempData["Success"] = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? "تم حفظ البائع." : "Vendor saved.";
        return RedirectToAction(nameof(Index));
    }

    // Only staff accounts with a dedicated vendor role are eligible.
    private IQueryable<User> EligibleUsers() => db.Users.Where(u => u.IsActive &&
        u.Role != null && u.Role.IsActive &&
        (u.Role.RoleName == VendorRoles.Owner || u.Role.RoleName == VendorRoles.Manager ||
         u.Role.RoleName == VendorRoles.Staff));

    [HttpGet]
    public async Task<IActionResult> Members(int id, string? search, int page = 1, CancellationToken ct = default)
    {
        var vendor = await db.Vendors.AsNoTracking().SingleOrDefaultAsync(v => v.VendorId == id, ct);
        if (vendor == null) return NotFound();
        const int size = 25;
        page = Math.Clamp(page, 1, 100000);
        var rows = await db.VendorUsers.AsNoTracking().Where(m => m.VendorId == id)
            .OrderBy(m => m.UserId).Skip((page - 1) * size).Take(size + 1)
            .Select(m => new VendorMembersViewModel.MemberRow
            {
                UserId = m.UserId, Name = m.User.FullName, Email = m.User.Email,
                Role = m.User.Role == null ? null : m.User.Role.RoleName, IsActive = m.IsActive,
                IsEligible = m.User.IsActive && m.User.Role != null && m.User.Role.IsActive &&
                    (m.User.Role.RoleName == VendorRoles.Owner || m.User.Role.RoleName == VendorRoles.Manager ||
                     m.User.Role.RoleName == VendorRoles.Staff)
            }).ToListAsync(ct);
        var model = new VendorMembersViewModel
        {
            VendorId = id, VendorName = vendor.Name, Search = search, Page = page,
            HasNext = rows.Count > size, Members = rows.Take(size).ToList()
        };
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search) && search.Length >= 2 && search.Length <= 150)
        {
            model.Candidates = await EligibleUsers().AsNoTracking()
                .Where(u => (u.FullName != null && u.FullName.Contains(search)) ||
                    (u.FullNameArb != null && u.FullNameArb.Contains(search)) ||
                    (u.Email != null && u.Email.Contains(search)))
                .Where(u => !db.VendorUsers.Any(m => m.VendorId == id && m.UserId == u.UserId))
                .OrderBy(u => u.UserId).Take(size)
                .Select(u => new VendorMembersViewModel.MemberRow
                {
                    UserId = u.UserId, Name = u.FullName, Email = u.Email, Role = u.Role!.RoleName,
                    IsEligible = true
                }).ToListAsync(ct);
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> AssignMember(int id, int userId, CancellationToken ct)
    {
        // Serialize membership creation with eligibility checks. Runtime vendor access must recheck them.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Vendors.AnyAsync(v => v.VendorId == id && v.Status != "Suspended", ct))
            return BadRequest("Vendor does not exist or is suspended.");
        if (!await EligibleUsers().AnyAsync(u => u.UserId == userId, ct))
            return BadRequest("Only active vendor-role users can be assigned.");
        if (await db.VendorUsers.AnyAsync(m => m.VendorId == id && m.UserId == userId, ct))
            return Conflict("Membership already exists.");
        db.VendorUsers.Add(new VendorUser { VendorId = id, UserId = userId });
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Conflict("Membership already exists.");
        }
        TempData["Success"] = MemberMessage("Vendor user assigned.", "تم تعيين مستخدم البائع.");
        return RedirectToAction(nameof(Members), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ChangeMemberStatus(int id, int userId, bool expectedActive, bool active, CancellationToken ct)
    {
        if (expectedActive == active) return BadRequest();
        var query = db.VendorUsers.Where(m => m.VendorId == id && m.UserId == userId && m.IsActive == expectedActive);
        if (active)
            query = query.Where(m => m.Vendor.Status != "Suspended" && EligibleUsers().Any(u => u.UserId == m.UserId));
        var updated = await query.ExecuteUpdateAsync(s => s.SetProperty(m => m.IsActive, active), ct);
        if (updated != 1) return Conflict("Membership changed or user is not eligible.");
        TempData["Success"] = MemberMessage("Membership updated.", "تم تحديث العضوية.");
        return RedirectToAction(nameof(Members), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Listings(int id, string? search, int page = 1, CancellationToken ct = default)
    {
        var vendor = await db.Vendors.AsNoTracking().SingleOrDefaultAsync(v => v.VendorId == id, ct);
        if (vendor == null) return NotFound();
        return View(await VendorListingQueries.ReadPageAsync(db, id, vendor.Name, search, page, false, true, ct));
    }

    [HttpPost]
    public async Task<IActionResult> ChangeListingStatus(int id, int listingId, string version, string status, CancellationToken ct)
    {
        if (status is not ("Approved" or "Inactive") ||
            !VendorListingQueries.TryVersion(version, out var expected)) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var listing = await db.VendorListings.Include(l => l.Product).Include(l => l.Vendor)
            .SingleOrDefaultAsync(l => l.VendorId == id && l.VendorListingId == listingId, ct);
        if (listing == null) return NotFound();
        if (!listing.RowVersion.SequenceEqual(expected)) return Conflict("Listing changed; reload.");
        if (status == "Approved" && (listing.Vendor.Status != "Approved" || !listing.Product.IsActive ||
            listing.Status is not ("Pending" or "Inactive"))) return BadRequest();
        if (status == "Inactive" && listing.Status == "Inactive") return BadRequest();
        listing.Status = status;
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return Conflict("Listing changed; reload."); }
        TempData["Success"] = MemberMessage("Listing status updated.", "تم تحديث حالة العرض.");
        return RedirectToAction(nameof(Listings), new { id });
    }

    private static string MemberMessage(string en, string ar) =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? ar : en;

    [HttpPost]
    public async Task<IActionResult> ChangeStatus(int id, string expectedStatus, string status, CancellationToken ct)
    {
        if ((expectedStatus, status) is not (("Pending", "Approved") or ("Approved", "Suspended") or ("Suspended", "Approved")))
            return BadRequest();
        var updated = await db.Vendors.Where(v => v.VendorId == id && v.Status == expectedStatus)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, status), ct);
        if (updated != 1) return Conflict("Vendor status changed; reload and try again.");
        TempData["Success"] = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? "تم تحديث حالة البائع." : "Vendor status updated.";
        return RedirectToAction(nameof(Index));
    }
}
