using DocumentFormat.OpenXml.InkML;
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

        public AccountController(EcommerceDbContext context)
        {
            _context = context;
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
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == model.Email && u.PasswordHash == model.Password && u.IsActive);

            if (user == null)
            {
                ViewBag.Error = "Invalid email or password";
                return View(model);
            }

            // ✅ Only allow Admin
            if (user.Role.RoleName != "Admin")
            {
                ViewBag.Error = "Access Denied. Only Admin can login.";
                return View(model);
            }

            // ✅ Create claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.RoleName),
                new Claim("UserId", user.UserId.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // ✅ Set cookie options (persistent or session)
            var authProperties = new AuthenticationProperties
            { 
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(3), // or as needed
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties
            );

            return RedirectToAction("Index", "AdminDashboard");
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
