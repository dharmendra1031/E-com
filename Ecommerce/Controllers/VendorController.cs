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
    public async Task<IActionResult> PaymentReview(int page=1,CancellationToken ct=default)
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
        return View(rows.Take(25).ToList());
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
