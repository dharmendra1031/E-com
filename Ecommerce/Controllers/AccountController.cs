using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceWebApplication.Controllers
{
   

    public class AccountController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly IConfiguration _configuration;

        public AccountController(EcommerceDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ✅ GET Login Page
        [HttpGet]
        public IActionResult Login()
        {
            var data = _context.CompanyDetails
          .Select(c => new LoginViewModel
          {
              CompanyName = c.CompanyName,
              LogoBase64 = c.Logo != null
                  ? "data:image/png;base64," + Convert.ToBase64String(c.Logo)
                  : null
          })
          .FirstOrDefault();
            return View(data);
        }

        // ✅ POST Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var candidates = await _context.Users.AsNoTracking().Include(u => u.Role)
                .Where(u => u.Email == model.Email && u.IsActive).Take(2).ToListAsync();
            var user = candidates.Count == 1 ? candidates[0] : null;
            var allowLegacy = _configuration.GetValue("Authentication:AllowLegacyStaffPasswords", false);
            var role = user?.Role?.RoleName;
            var restricted = AdminStaffAccess.IsRestricted(role);
            var allowedRole = role == "Admin" || restricted &&
                HttpContext.RequestServices.GetRequiredService<AdminStaffAccess>().Enabled;
            if (user?.Role is not { IsActive: true } || !allowedRole ||
                !StaffPassword.Verify(user.PasswordHash, model.Password, allowLegacy, out var upgrade))
            {
                ViewBag.Error = "Invalid email or password";
                return View(model);
            }
            if (upgrade)
            {
                var oldPassword = user.PasswordHash;
                var newHash = StaffPassword.Hash(model.Password);
                var updated = await _context.Users.Where(u => u.UserId == user.UserId && u.IsActive &&
                    u.RoleId == user.RoleId && u.PasswordHash == oldPassword &&
                    u.Role != null && u.Role.IsActive && u.Role.RoleName == role)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.PasswordHash, newHash));
                if (updated != 1)
                {
                    ViewBag.Error = "Login details changed. Please try again.";
                    return View(model);
                }
                user.PasswordHash = newHash;
            }

            // ✅ Create claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.RoleName),
                new Claim("UserId", user.UserId.ToString())
            };

            if (restricted)
            {
                claims.Add(new Claim(AdminStaffAccess.CredentialClaim, VendorPortalAuth.CredentialVersion(user.PasswordHash!)));
                claims.Add(new Claim(AdminStaffAccess.RoleIdClaim, user.RoleId.ToString()));
            }
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // ✅ Set cookie options (persistent or session)
            var authProperties = new AuthenticationProperties
            { 
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(3), // or as needed
                AllowRefresh = true
            };

            var destination = restricted ? await HttpContext.RequestServices.GetRequiredService<AdminStaffAccess>()
                .FirstPageAsync(principal, HttpContext.RequestAborted) : ("AdminDashboard", "Index");
            if (destination == null)
            {
                ViewBag.Error = "No available Admin page permissions.";
                return View(model);
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties
            );

            return RedirectToAction(destination.Value.Item2, destination.Value.Item1);
        }


        // ✅ Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // ✅ Access Denied Page
        public IActionResult AccessDenied()
        {
            return Content("Access Denied");
        }
    }
}
