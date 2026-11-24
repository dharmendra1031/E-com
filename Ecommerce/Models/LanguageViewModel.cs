using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace EcommerceWebApplication.Models
{

    public class LanguageViewModel
    {
        public string SelectedLanguage { get; set; }
        public string lang { get; set; }
        public List<SelectListItem> Languages { get; set; }

        public LanguageViewModel()
        {
            Languages = new List<SelectListItem>
            {
                new SelectListItem { Text = "Arabic", Value = "ar" },
                new SelectListItem { Text = "English", Value = "en" }
            };
        }
    }
}
