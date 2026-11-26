using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceWebApplication.Controllers;

[Route("vendor-portal")]
[Authorize(Policy = VendorPortalAuth.Policy)]
[AutoValidateAntiforgeryToken]
public class VendorPortalController(EcommerceDbContext db, VendorPortalAccess access) : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login() => access.Enabled ? View(new VendorLoginForm()) : NotFound();

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(VendorPortalAuth.LoginRatePolicy)]
    public async Task<IActionResult> Login(VendorLoginForm form, CancellationToken ct)
    {
        if (!access.Enabled) return NotFound();
        if (!ModelState.IsValid) return View(form);
        var candidates = await db.Users.AsNoTracking().Include(u => u.Role)
            .Where(u => u.IsActive && u.Email == form.Email).Take(2).ToListAsync(ct);
        var user = candidates.Count == 1 ? candidates[0] : null;
        if (user?.Role is not { IsActive: true } || !VendorRoles.IsVendorRole(user.Role.RoleName) ||
            !StaffPassword.Verify(user.PasswordHash, form.Password, false, out _) ||
            !await access.Memberships(user.UserId).AnyAsync(ct))
        {
            ModelState.AddModelError("", System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
                ? "بيانات الدخول غير صحيحة أو الحساب غير متاح." : "Invalid credentials or account unavailable.");
            form.Password = "";
            return View(form);
        }
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("UserId", user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.FullName ?? ""),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Role, user.Role.RoleName!),
            new Claim(VendorPortalAuth.CredentialClaim, VendorPortalAuth.CredentialVersion(user.PasswordHash!))
        }, VendorPortalAuth.Scheme);
        await HttpContext.SignInAsync(VendorPortalAuth.Scheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(3) });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!access.Enabled) return NotFound();
        if (!VendorPortalAuth.TryGetUserId(User, out var userId)) return Forbid(VendorPortalAuth.Scheme);
        var vendors = await access.Memberships(userId).OrderBy(m => m.Vendor.Name).ThenBy(m => m.VendorId)
            .Select(m => new VendorPortalStore { VendorId = m.VendorId, Code = m.Vendor.VendorCode,
                Name = m.Vendor.Name, NameArb = m.Vendor.NameArb }).ToListAsync(ct);
        if (vendors.Count == 1) return RedirectToAction(nameof(Dashboard), new { id = vendors[0].VendorId });
        return View(vendors);
    }

    [HttpGet("store/{id:int}")]
    public async Task<IActionResult> Dashboard(int id, CancellationToken ct)
    {
        if (!access.Enabled) return NotFound();
        if (!VendorPortalAuth.TryGetUserId(User, out var userId)) return Forbid(VendorPortalAuth.Scheme);
        var store = await access.Memberships(userId).Where(m => m.VendorId == id)
            .Select(m => new VendorPortalStore { VendorId = m.VendorId, Code = m.Vendor.VendorCode,
                Name = m.Vendor.Name, NameArb = m.Vendor.NameArb }).SingleOrDefaultAsync(ct);
        if (store == null) return NotFound();
        return View(store);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(VendorPortalAuth.Scheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("denied")]
    public IActionResult Denied() => StatusCode(StatusCodes.Status403Forbidden);
}
