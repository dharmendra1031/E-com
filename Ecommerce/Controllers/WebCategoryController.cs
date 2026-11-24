using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EcommerceWebApplication.Controllers
{
    public class WebCategoryController : Controller
    {
        private readonly EcommerceDbContext _context;

        public WebCategoryController(EcommerceDbContext context)
        {
            _context = context;
        }
        public IActionResult Index(int? categoryId, int? subCategoryId)
        {
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SelectedSubCategoryId = subCategoryId;
            return View();
        }

        public IActionResult CategoryPartial()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            var categories = _context.Categories
                .Where(x => x.IsActive)
                .Select(x => new CategoryListViewModel
                {
                    CategoryId = x.CategoryId,
                    //CName = x.CName,
                    CName = isArabic ? x.CNameArb : x.CName,
                    CategoryImageBase64 = x.CategoryImage != null
                        ? "data:image/png;base64," + Convert.ToBase64String(x.CategoryImage)
                        : "/images/default-category.png"
                })
                .ToList();

            return PartialView("_CategoryPartial", categories);
        }
        //public IActionResult ProductPartial(int? categoryId, int? subCategoryId)
        //{
        //    var products = _context.Products
        //        .Include(p => p.ProductImages)
        //        .Where(p => p.IsActive
        //            && (!categoryId.HasValue || p.CategoryId == categoryId)
        //            && (!subCategoryId.HasValue || p.SubCategoryId == subCategoryId))
        //        .Select(p => new ProductListViewModel
        //        {
        //            ProductId = p.ProductId,
        //            Name = p.Name,
        //            Price = p.Price,
        //            Stock =
        //                (_context.ProductStocks.Where(s => s.ProductId == p.ProductId)
        //                    .Sum(s => (int?)s.StockQty) ?? 0)
        //              - (_context.OrderItems.Where(o => o.ProductId == p.ProductId)
        //                    .Sum(o => (int?)o.Quantity) ?? 0),

        //            ProductImageBase64 = p.ProductImages
        //                .OrderBy(x => x.ImageId)
        //                .Select(x =>
        //                    "data:" + x.ContentType + ";base64," +
        //                    Convert.ToBase64String(x.ImageData))
        //                .FirstOrDefault()
        //        })
        //        .ToList();

        //    return PartialView("_ProductsPartial", products);
        //}
        public IActionResult ProductPartial(int? categoryId, int? subCategoryId, string sortBy = "newest")
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            var query = _context.Products
     .Include(p => p.ProductImages)
     .Where(p => p.IsActive)
     .AsQueryable();

            // 🔹 Conditional filtering
            if (categoryId.HasValue && subCategoryId.HasValue)
            {
                // dono selected
                query = query.Where(p => p.CategoryId == categoryId && p.SubCategoryId == subCategoryId);
            }
            else if (categoryId.HasValue)
            {
                // sirf category
                query = query.Where(p => p.CategoryId == categoryId);
            }
            else if (subCategoryId.HasValue)
            {
                // sirf subcategory
                query = query.Where(p => p.SubCategoryId == subCategoryId);
            }
            //        var query = _context.Products
            //.Include(p => p.ProductImages)
            //.Where(p => p.IsActive)
            //.AsQueryable();

            //        if (subCategoryId.HasValue)
            //        {
            //            // 🔹 SubCategory priority le
            //            query = query.Where(p => p.SubCategoryId == subCategoryId);
            //        }
            //        else if (categoryId.HasValue)
            //        {
            //            // 🔹 Sirf Category ho to category se filter
            //            query = query.Where(p => p.CategoryId == categoryId);
            //        }

            // ✅ SORTING (taken from GetProducts)
            query = sortBy switch
            {
                "low-high" => query.OrderBy(p => p.Price),
                "high-low" => query.OrderByDescending(p => p.Price),
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            
            var products = query.Select(p => new ProductListViewModel
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
                    (_context.ProductStocks.Where(s => s.ProductId == p.ProductId)
                        .Sum(s => (int?)s.StockQty) ?? 0)
                  - (_context.OrderItems.Where(o => o.ProductId == p.ProductId)
                        .Sum(o => (int?)o.Quantity) ?? 0),

                ProductImageBase64 = p.ProductImages
                    .OrderBy(x => x.ImageId)
                    .Select(x =>
                        "data:" + x.ContentType + ";base64," +
                        Convert.ToBase64String(x.ImageData))
                    .FirstOrDefault(),
               

            }).ToList();
            

            return PartialView("_ProductsPartial", products);
        }


        public IActionResult SubCategoryCardPartial(int? categoryId)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            // 🔹 Agar categoryId null hai to default category uthao
            if (!categoryId.HasValue)
            {
                categoryId = _context.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.CategoryId)   // TOP / FIRST
                    .Select(c => c.CategoryId)
                    .FirstOrDefault();
            }

            var data = _context.SubCategories
                .Where(x => x.IsActive && x.ParentCategoryId == categoryId)
                .Select(x => new SubCategoryListViewModel
                {
                    SubCategoryId = x.SubCategoryId,
                    SCName = isArabic ? x.SCNameArb : x.SCName,
                    //SCName = x.SCName,
                    SubCategoryImageBase64 = x.SubCategoryImage != null
                        ? "data:image/png;base64," + Convert.ToBase64String(x.SubCategoryImage)
                        : "/images/default-category.png"
                })
                .ToList();

            return PartialView("_SubCategoryPartial", data);
        }

        [HttpGet]
        public IActionResult GetCategories()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            var categories = _context.Categories
                .Where(c => c.IsActive)
                .Select(c => new
                {
                    categoryId = c.CategoryId,
                    cName = isArabic ? c.CNameArb : c.CName,
                    subCategories = c.SubCategories
                        .Where(s => s.IsActive)
                        .Select(s => new
                        {
                            subCategoryId = s.SubCategoryId,
                            scName = isArabic ? s.SCNameArb : s.SCName,
                            ImageBase64 = s.SubCategoryImage != null
                ? "data:image/png;base64," +
                  Convert.ToBase64String(s.SubCategoryImage)
                : null
                        }).ToList()
                }).ToList();

            return Json(categories);
        }
        [HttpGet]
        public IActionResult GetSubCategoriesByCategoryId(int id)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            var subCategories = _context.SubCategories
                .Where(s => s.ParentCategoryId == id && s.IsActive)
                .Select(s => new
                {
                    subCategoryId = s.SubCategoryId,
                      scName = isArabic ? s.SCNameArb : s.SCName,
                    ImageBase64 = s.SubCategoryImage != null
                ? "data:image/png;base64," +
                  Convert.ToBase64String(s.SubCategoryImage)
                : null
                }).ToList();

            return Json(subCategories);
        }
        [HttpGet]
        public IActionResult GetNavSubCategories()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            var subCategories = _context.Categories
                .Where(x => x.IsActive)
                .Select(x => new
                {
                    CategoryId = x.CategoryId,
                    CName = isArabic ? x.CNameArb : x.CName,
                      ImageBase64 = x.CategoryIcon != null
                ? "data:image/png;base64," + 
                  Convert.ToBase64String(x.CategoryIcon)
                : null
                })
                .ToList();

            return Json(subCategories);
        }

        //public JsonResult GetSubCategories(int categoryId)
        //{
        //    var culture = System.Globalization.CultureInfo.CurrentCulture;
        //    bool isArabic = culture.TextInfo.IsRightToLeft;
        //    var data = _context.SubCategories
        //        .Where(x => x.ParentCategoryId == categoryId)
        //        .Select(x => new {
        //            subCategoryId = x.SubCategoryId,
        //            CName = isArabic ? x.SCNameArb : x.SCName
        //        }).ToList();

        //    return Json(data);
        //}
    }
}
