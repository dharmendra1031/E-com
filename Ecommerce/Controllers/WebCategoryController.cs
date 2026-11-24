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
            if (subCategoryId.HasValue)
            {
                var parentCategoryId = _context.SubCategories
                    .Where(s => s.SubCategoryId == subCategoryId && s.IsActive && s.ParentCategory.IsActive)
                    .Select(s => (int?)s.ParentCategoryId)
                    .FirstOrDefault();
                if (parentCategoryId.HasValue)
                    categoryId = parentCategoryId;
                else
                    subCategoryId = null;
            }

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
                        ? "/StoreImage/Category/" + x.CategoryId
                        : "/images/default-category.png"
                })
                .ToList();

            return PartialView("_CategoryPartial", categories);
        }
        public IActionResult ProductPartial(int? categoryId, int? subCategoryId, string sortBy = "newest", int page = 1, int? brandId = null, decimal? minPrice = null, decimal? maxPrice = null, bool inStock = false, int? minRating = null)
        {
            const int pageSize = 24;
            if (page < 1 || page > int.MaxValue / pageSize) return BadRequest();
            bool isArabic = System.Globalization.CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var query = _context.Products.AsNoTracking().Where(p => p.IsActive);
            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId);
            if (subCategoryId.HasValue) query = query.Where(p => p.SubCategoryId == subCategoryId);
            if (!ModelState.IsValid || minPrice < 0 || maxPrice < 0 ||
                (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice) ||
                (minRating.HasValue && (minRating < 1 || minRating > 5))) return BadRequest();
            if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId);
            if (minPrice.HasValue) query = query.Where(p => p.Price >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(p => p.Price <= maxPrice.Value);
            if (minRating.HasValue) query = query.Where(p => (p.ProductReviews.Average(r => (double?)r.Rating) ?? 0) >= minRating.Value);
            if (inStock) query = query.Where(p =>
                (_context.ProductStocks.Where(s => s.ProductId == p.ProductId).Sum(s => (int?)s.StockQty) ?? 0)
                > (_context.OrderItems.Where(o => o.ProductId == p.ProductId).Sum(o => (int?)o.Quantity) ?? 0));
            var ordered = sortBy switch
            {
                "low-high" => query.OrderBy(p => p.Price).ThenBy(p => p.ProductId),
                "high-low" => query.OrderByDescending(p => p.Price).ThenBy(p => p.ProductId),
                _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId)
            };
            var products = ordered.Skip((page - 1) * pageSize).Take(pageSize + 1)
                .Select(p => new ProductListViewModel
                {
                    ProductId = p.ProductId,
                    Name = isArabic ? p.NameArb : p.Name,
                    Price = p.Price,
                    AverageRating = p.ProductReviews.Average(r => (double?)r.Rating) ?? 0,
                    ReviewCount = p.ProductReviews.Count(),
                    Stock = (_context.ProductStocks.Where(s => s.ProductId == p.ProductId).Sum(s => (int?)s.StockQty) ?? 0)
                        - (_context.OrderItems.Where(o => o.ProductId == p.ProductId).Sum(o => (int?)o.Quantity) ?? 0),
                    ProductImageId = p.ProductImages.OrderBy(img => img.ImageId)
                        .Select(img => (int?)img.ImageId).FirstOrDefault()
                }).ToList();
            ViewBag.NextPage = products.Count > pageSize ? page + 1 : (int?)null;
            ViewBag.TotalProducts = query.Count();
            return PartialView("_ProductsPartial", products.Take(pageSize).ToList());
        }

        [HttpGet]
        public IActionResult FilterBrands(int categoryId, int? subCategoryId)
        {
            bool ar = System.Globalization.CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var brands = _context.Products.AsNoTracking()
                .Where(p => p.IsActive && p.CategoryId == categoryId && p.BrandId != null &&
                    (!subCategoryId.HasValue || p.SubCategoryId == subCategoryId))
                .Select(p => new { id = p.BrandId, name = ar ? p.Brand.BrandNameAr : p.Brand.BrandNameAEn })
                .Distinct().OrderBy(b => b.name).ToList();
            return Ok(brands);
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
                        ? "/StoreImage/SubCategory/" + x.SubCategoryId
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
                ? "/StoreImage/SubCategory/" + s.SubCategoryId
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
                ? "/StoreImage/SubCategory/" + s.SubCategoryId
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
                ? "/StoreImage/CategoryIcon/" + x.CategoryId
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
