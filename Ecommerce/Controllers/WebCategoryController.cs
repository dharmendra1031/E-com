using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EcommerceWebApplication.Controllers
{
    public class WebCategoryController : Controller
    {
        private readonly EcommerceDbContext _context;

        private readonly IConfiguration _configuration;
        public WebCategoryController(EcommerceDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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
        [HttpGet]
        public IActionResult Search(string? term, string sortBy = "newest", int page = 1, bool inStock = false)
        {
            ViewBag.SearchTerm = term?.Trim() ?? "";
            ViewBag.SortBy = sortBy;
            ViewBag.InStock = inStock;
            ViewBag.Page = page;
            if (string.IsNullOrWhiteSpace(term))
            {
                ViewBag.TotalProducts = 0;
                return View(new List<ProductListViewModel>());
            }
            return CatalogProducts(null, null, sortBy, page, null, null, null, inStock, null, false, term, true);
        }

        public IActionResult ProductPartial(int? categoryId, int? subCategoryId, string sortBy = "newest", int page = 1, int? brandId = null, decimal? minPrice = null, decimal? maxPrice = null, bool inStock = false, int? minRating = null, bool brandedOnly = false) =>
            CatalogProducts(categoryId, subCategoryId, sortBy, page, brandId, minPrice, maxPrice, inStock, minRating, brandedOnly, null, false);

        private IActionResult CatalogProducts(int? categoryId, int? subCategoryId, string sortBy, int page, int? brandId,
            decimal? minPrice, decimal? maxPrice, bool inStock, int? minRating, bool brandedOnly, string? term, bool searchPage)
        {
            const int pageSize = 24;
            if (page < 1 || page > int.MaxValue / pageSize) return BadRequest();
            bool isArabic = System.Globalization.CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var query = _context.Products.AsNoTracking().Where(p => p.IsActive);
            if (searchPage)
            {
                term = term?.Trim();
                if (string.IsNullOrEmpty(term) || term.Length > 100) return BadRequest();
                var keyword = term;
                query = query.Where(p => p.Name.Contains(keyword) || p.NameArb.Contains(keyword) ||
                    (p.SKUId != null && p.SKUId.Contains(keyword)) ||
                    (p.ProductsModel != null && p.ProductsModel.Contains(keyword)) ||
                    _context.Brand.Any(b => b.BrandId == p.BrandId &&
                        (b.BrandNameAEn.Contains(keyword) || b.BrandNameAr.Contains(keyword))));
            }
            if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId);
            if (subCategoryId.HasValue) query = query.Where(p => p.SubCategoryId == subCategoryId);
            if (!ModelState.IsValid || minPrice < 0 || maxPrice < 0 ||
                (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice) ||
                (minRating.HasValue && (minRating < 1 || minRating > 5))) return BadRequest();
            if (brandedOnly) query = query.Where(p => p.BrandId.HasValue && p.Brand != null);
            if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId);
            if (minRating.HasValue) query = query.Where(p => (p.ProductReviews.Average(r => (double?)r.Rating) ?? 0) >= minRating.Value);
            var preview = MarketplaceOffers.Enabled(_configuration);
            if (!preview)
            {
                if (minPrice.HasValue) query = query.Where(p => p.Price >= minPrice.Value);
                if (maxPrice.HasValue) query = query.Where(p => p.Price <= maxPrice.Value);
                if (inStock) query = query.Where(p =>
                    (_context.ProductStocks.Where(s => s.ProductId == p.ProductId).Sum(s => (int?)s.StockQty) ?? 0)
                    > (_context.OrderItems.Where(o => o.ProductId == p.ProductId).Sum(o => (int?)o.Quantity) ?? 0));
            }

            var cards = query.Select(p => new ProductListViewModel
            {
                ProductId = p.ProductId, CreatedAt = p.CreatedAt,
                BrandName = _context.Brand.Where(b => b.BrandId == p.BrandId)
                    .Select(b => isArabic && !string.IsNullOrEmpty(b.BrandNameAr) ? b.BrandNameAr : b.BrandNameAEn)
                    .FirstOrDefault(),
                Name = isArabic && !string.IsNullOrEmpty(p.NameArb) ? p.NameArb : p.Name,
                Price = p.Price,
                AverageRating = p.ProductReviews.Average(r => (double?)r.Rating) ?? 0,
                ReviewCount = p.ProductReviews.Count(),
                Stock = (_context.ProductStocks.Where(s => s.ProductId == p.ProductId).Sum(s => (int?)s.StockQty) ?? 0)
                    - (_context.OrderItems.Where(o => o.ProductId == p.ProductId).Sum(o => (int?)o.Quantity) ?? 0),
                ProductImageId = p.ProductImages.OrderBy(img => img.ImageId)
                    .Select(img => (int?)img.ImageId).FirstOrDefault()
            });
            if (preview)
            {
                // Join grouped eligible offers before filtering, sorting and paging.
                var summaries = MarketplaceOffers.Summaries(_context);
                cards = from card in cards
                        join offer in summaries on card.ProductId equals offer.ProductId into matches
                        from offer in matches.DefaultIfEmpty()
                        select new ProductListViewModel
                        {
                            ProductId = card.ProductId, CreatedAt = card.CreatedAt,
                            BrandName = card.BrandName, Name = card.Name, Price = card.Price,
                            AverageRating = card.AverageRating, ReviewCount = card.ReviewCount,
                            Stock = card.Stock, ProductImageId = card.ProductImageId,
                            LowestSellerPrice = (decimal?)offer.LowestPrice,
                            AvailableSellerCount = (int?)offer.SellerCount ?? 0
                        };
                if (minPrice.HasValue) cards = cards.Where(p => p.LowestSellerPrice >= minPrice.Value);
                if (maxPrice.HasValue) cards = cards.Where(p => p.LowestSellerPrice <= maxPrice.Value);
                if (inStock) cards = cards.Where(p => p.AvailableSellerCount > 0);
            }
            var ordered = sortBy switch
            {
                "low-high" when preview => cards.OrderBy(p => p.LowestSellerPrice == null).ThenBy(p => p.LowestSellerPrice).ThenBy(p => p.ProductId),
                "high-low" when preview => cards.OrderBy(p => p.LowestSellerPrice == null).ThenByDescending(p => p.LowestSellerPrice).ThenBy(p => p.ProductId),
                "low-high" => cards.OrderBy(p => p.Price).ThenBy(p => p.ProductId),
                "high-low" => cards.OrderByDescending(p => p.Price).ThenBy(p => p.ProductId),
                _ => cards.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId)
            };
            var products = ordered.Skip((page - 1) * pageSize).Take(pageSize + 1).ToList();
            ViewBag.NextPage = products.Count > pageSize ? page + 1 : (int?)null;
            ViewBag.TotalProducts = cards.Count();
            ViewBag.SellerPreview = preview;
            var items = products.Take(pageSize).ToList();
            if (searchPage) return View("Search", items);
            return PartialView("_ProductsPartial", items);
        }

        [HttpGet]
        public IActionResult FilterBrands(int categoryId, int? subCategoryId)
        {
            bool ar = System.Globalization.CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var products = _context.Products.AsNoTracking()
                .Where(p => p.IsActive && p.CategoryId == categoryId && p.BrandId.HasValue);
            if (subCategoryId.HasValue)
                products = products.Where(p => p.SubCategoryId == subCategoryId.Value);
            var brands = _context.Brand.AsNoTracking()
                .Where(b => products.Any(p => p.BrandId == b.BrandId))
                .Select(b => new { id = b.BrandId, name = ar && !string.IsNullOrEmpty(b.BrandNameAr) ? b.BrandNameAr : b.BrandNameAEn })
                .OrderBy(b => b.name).ToList();
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
