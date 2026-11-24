using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace EcommerceWebApplication.Helper
{
    public static class LanguageHandler
    {
        public static string GetLanguage(HttpContext context)
        {
            var cultureFeature = context.Features.Get<IRequestCultureFeature>();
            return cultureFeature?.RequestCulture.Culture.Name ?? "en";
        }

        public static void SetLanguage(HttpContext context, string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
                lang = "en";

            var culture = new CultureInfo(lang);
            var requestCulture = new RequestCulture(culture);

            context.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName, // this is .AspNetCore.Culture
                CookieRequestCultureProvider.MakeCookieValue(requestCulture),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    Path = "/", // ✅ required to make cookie apply site-wide
                    HttpOnly = false,
                    IsEssential = true,
                    Secure = false // change to true in production with HTTPS
                });
        }
    }
}