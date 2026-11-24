using Microsoft.AspNetCore.Mvc;
using EcommerceWebApplication.Helper;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using EcommerceWebApplication.Models;

namespace EcommerceWebApplication.Helper
{
    public class LanguageViewComponent : ViewComponent
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LanguageViewComponent(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public IViewComponentResult Invoke()
        {
            string lang = LanguageHandler.GetLanguage(_httpContextAccessor.HttpContext);
            LanguageHandler.SetLanguage(_httpContextAccessor.HttpContext, lang);

            var model = new LanguageViewModel
            {
                SelectedLanguage = lang
            };

            return View("/Views/Index.cshtml", model);


        }
    }

}


