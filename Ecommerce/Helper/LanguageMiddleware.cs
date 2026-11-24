using EcommerceWebApplication.Helper;
using System.Globalization;

namespace EcommerceWebApplication.Helper
{

    public class LanguageMiddleware
    {
        private readonly RequestDelegate _next;

        //public LanguageMiddleware(RequestDelegate next)
        //{
        //    _next = next;
        //}

        //public async Task InvokeAsync(HttpContext context)
        //{
        //    var lang = LanguageHandler.GetLanguage(context);
        //    CultureInfo.CurrentCulture = new CultureInfo(lang);
        //    CultureInfo.CurrentUICulture = new CultureInfo(lang);

        //    // Allow the request to continue to the next middleware
        //    await _next(context);
        //}
    }
}