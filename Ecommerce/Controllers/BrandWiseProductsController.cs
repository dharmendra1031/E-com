using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
    public class BrandWiseProductsController : Controller
    {
        private readonly EcommerceDbContext _context;
        public BrandWiseProductsController(EcommerceDbContext context) => _context = context;

        public IActionResult Index(int? brandId)
        {
            bool ar = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var brands = _context.Brand.AsNoTracking().OrderBy(b => b.BrandId)
                .Select(b => new BrandWiseProductsViewModel
                {
                    BrandId = b.BrandId,
                    BrandName = ar ? b.BrandNameAr : b.BrandNameAEn,
                    BrandImageBase64 = b.BrandLogo != null ? "/StoreImage/Brand/" + b.BrandId : null
                }).ToList();
            ViewBag.SelectedBrandId = brandId.HasValue && brands.Any(b => b.BrandId == brandId.Value) ? brandId : null;
            return View(brands);
        }
    }
}
