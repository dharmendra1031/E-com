using EcommerceWebApplication.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    public class AboutCompanyController : Controller
    {
        private readonly EcommerceDbContext _context;
        public AboutCompanyController(EcommerceDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> About()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            // 1. Company info
            var company = await _context.CompanyDetails.FirstOrDefaultAsync();
            if (company == null)
                return NotFound();

            // 2. About Us info (can be multiple entries)
            var aboutUsList = await _context.AboutUs
                                            .Where(a => a.IsActive == 1)
                                            .ToListAsync();

            // 3. Dynamic achievements
            var customerCount = await _context.Customers.CountAsync(c => c.IsActive);
            var productCount = await _context.Products.CountAsync();


            //        ViewBag.Achievements = new List<dynamic>
            //{
            //    new { Title = "Customers", Value = customerCount.ToString("N0"), IconClass = "fa-users" },
            //    new { Title = "Products", Value = productCount.ToString("N0"), IconClass = "fa-box" },

            //};
            ViewBag.Achievements = new List<dynamic>
{
    new {
        Title = isArabic ? "العملاء" : "Customers",
        Value = customerCount.ToString("N0"),
        IconClass = "fa-users"
    },
    new {
        Title = isArabic ? "المنتجات" : "Products",
        Value = productCount.ToString("N0"),
        IconClass = "fa-box"
    }
};

            // 4. Pass AboutUs along with company using ViewBag (or ViewModel if you want strongly typed)
            ViewBag.AboutUs = aboutUsList;

            return View(company);
        }

    }
}
