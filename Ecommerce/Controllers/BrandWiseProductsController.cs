using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    public class BrandWiseProductsController : Controller
    {
        private readonly EcommerceDbContext _context;

        public BrandWiseProductsController(EcommerceDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(int? brandId)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            // 🔹 ALL BRANDS (for horizontal scroll)
            var brands = _context.Brand
        .Select(b => new BrandWiseProductsViewModel
        {
            BrandId = b.BrandId,
            //BrandName = b.BrandNameAEn,
            BrandName = isArabic ? b.BrandNameAr : b.BrandNameAEn,
            BrandImageBase64 = b.BrandLogo != null
                ? "/StoreImage/Brand/" + b.BrandId
                : null
        })
        .ToList();

            // 🔹 PRODUCTS (filtered by brandId)
            var products = _context.Brand
                .Where(b => !brandId.HasValue || b.BrandId == brandId)
                .Select(b => new BrandWiseProductsViewModel
                {
                    
                    BrandId = b.BrandId,
                    //BrandName = b.BrandNameAEn,
                    BrandName = isArabic ? b.BrandNameAr : b.BrandNameAEn,
                    Products = _context.Products
                        .Where(p => p.IsActive && p.BrandId == b.BrandId)
                        .Select(p => new ProductListViewModel
                        {
                            Rating = _context.ProductReviews
                                  .Where(r => r.ProductId == p.ProductId)
                                  .Select(r => r.Rating)
                                  .FirstOrDefault(),
                            ProductId = p.ProductId,
                            //Name = p.Name,
                            Name = isArabic ? p.NameArb : p.Name,
                            Price = p.Price,
                            Stock =
                                (_context.ProductStocks
                                    .Where(s => s.ProductId == p.ProductId)
                                    .Sum(s => (int?)s.StockQty) ?? 0)
                                -
                                (_context.OrderItems
                                    .Where(o => o.ProductId == p.ProductId)
                                    .Sum(o => (int?)o.Quantity) ?? 0),

                            ProductImageId = _context.ProductImages
                                .Where(img => img.ProductId == p.ProductId)
                                .OrderBy(img => img.ImageId)
                                .Select(img => (int?)img.ImageId)
                                .FirstOrDefault()
                        }).ToList()
                })
                .Where(x => x.Products.Any())
                .ToList();

            ViewBag.AllBrands = brands;
            ViewBag.SelectedBrandId = brandId;
            return View(products);
        }



    }
}
