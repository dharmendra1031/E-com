
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using System.Diagnostics;

namespace EcommerceWebApplication.Controllers
{
    
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IHtmlLocalizer<HomeController> _localizer;

        public HomeController(ILogger<HomeController> logger, IHtmlLocalizer<HomeController> localizer)
        {
            _logger = logger;
            _localizer = localizer;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult SetLanguage(string culture, string returnUrl = "/")
        {
            LanguageHandler.SetLanguage(HttpContext, culture);
            var direction = culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "ltr" : "rtl";
            Response.Cookies.Append(
                "LayoutDirection",
                direction,
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            return LocalRedirect(returnUrl);
        }
        //public IActionResult SetLanguage(string culture, string returnUrl = "/")
        //{
        //    Response.Cookies.Append(
        //         CookieRequestCultureProvider.DefaultCookieName,
        //         CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        //         new CookieOptions
        //         {
        //             Expires = DateTimeOffset.UtcNow.AddYears(1),
        //             SameSite = SameSiteMode.Lax,
        //             Secure = true
        //         }
        //     );
        //    return LocalRedirect(returnUrl ?? "/");

        //}

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
