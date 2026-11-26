using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
