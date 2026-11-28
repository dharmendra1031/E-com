using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    public class WebHomeController : Controller
    {
        private readonly EcommerceDbContext _context;

        private readonly IConfiguration _configuration;
        public WebHomeController(EcommerceDbContext context, IConfiguration configuration)
        { _context = context; _configuration = configuration; }
        public IActionResult Index()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            var banners = _context.Banner
                                  .Where(b => b.BannerImage != null)
                                  .OrderBy(b => b.DisplayOrder).ThenBy(b => b.BannerId)
                                  .Select(b => new BannerModel { BannerId = b.BannerId, Title = b.Title, TitleArabic = b.TitleArabic })
                                  .ToList();
            //var categories = _context.Categories
            //                  .Where(c => c.IsActive)
            //                  .OrderBy(c => c.CategoryId)
            //                  .ToList();

            //// Base64 image string add karo
            //foreach (var cat in categories)
            //{
            //    if (cat.CategoryImage != null)
            //        cat.ImageBase64 = $"data:image/png;base64,{Convert.ToBase64String(cat.CategoryImage)}";
            //    else
            //        cat.ImageBase64 = "/images/default-category.png";
            //}

            var categories = _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryId)
                .Select(c => new Category
                {
                    CategoryId = c.CategoryId,

                    // 🔥 CULTURE BASED NAME
                    CName = isArabic ? c.CNameArb : c.CName,

                    ImageBase64 = c.CategoryImage != null
                        ? "/StoreImage/Category/" + c.CategoryId
                        : "/images/default-category.png"
                })
                .ToList();

            var brands = _context.Brand
                         .OrderBy(d => d.BrandId)
                         .Select(d => new BrandViewModel
                         {
                             BrandId = d.BrandId,
                             //BrandNameAr = d.BrandNameAr,
                             //BrandNameAEn = d.BrandNameAEn,
                             BrandNameAEn = isArabic ? d.BrandNameAr : d.BrandNameAEn,


                             // 🔥 Base64 conversion
                             BrandlogoBase64 = d.BrandLogo != null
                                 ? "/StoreImage/Brand/" + d.BrandId
                                 : "/images/default-brand.png"
                         })
                         .ToList();


            var today = DateTime.Today;

            var weekendDeals = (from wd in _context.WekendDeals
                                join p in _context.Products
                                    on wd.ProductId equals p.ProductId
                                where wd.FromDate <= DateTime.Now
                      && wd.ToDate >= DateTime.Now && p.IsActive
                                select new WeekendDealProductVM
                                {
                                    DealId = wd.WdId,
                                    WdNameEn = wd.WdNameEn,
                                    WdNameAr = wd.WdNameAr,
                                    DiscPer = wd.DiscPer,
                                    FromDate = wd.FromDate,
                                    ToDate = wd.ToDate,

                                    ProductId = p.ProductId,
                                    ProductName = p.Name,
                                    ProductNameAr = p.NameArb,
                                    ProductPrice = p.Price,

                                    DiscountAmount = (p.Price * wd.DiscPer) / 100,
                                    OfferPrice = p.Price - ((p.Price * wd.DiscPer) / 100),

                                    ProductImagesBase64 = p.ProductImages
                                        .OrderBy(img => img.ImageId).Take(1)
                                        .Select(img => "/StoreImage/Product/" + img.ImageId)
                                        .ToList()
                                }).OrderByDescending(x => x.DealId)
                               .Take(6)
                                .ToList(); ;


            ViewBag.Categories = categories;
            ViewBag.Banners = banners;
            ViewBag.Brands = brands;
            HomeViewModel vm = new HomeViewModel();

            vm.WeekendDeals = weekendDeals;
            ViewBag.SellerSummaries = MarketplaceOffers.ForProducts(_context, weekendDeals.Where(d => d.ProductId.HasValue).Select(d => d.ProductId!.Value), _configuration);

            return View(vm);
        }
         
        public IActionResult _TopHeader()
        {
            var data = _context.CompanyDetails.FirstOrDefault();
            return PartialView("_TopheaderdetailsPartial", data);
        }


        public IActionResult GetCompanyDetails()
        {
            var company = _context.CompanyDetails.FirstOrDefault();

            return PartialView("_TopheaderdetailsPartial", company);
        }
        [HttpGet]
        public async Task<IActionResult> GetTopRatedProducts()
        {
            bool isArabic = System.Globalization.CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var topProducts = await _context.ProductReviews
                .Where(r => r.Product.IsActive)
                .GroupBy(r => r.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    AverageRating = g.Average(r => r.Rating),
                    TotalReviews = g.Count()
                })
                .OrderByDescending(x => x.AverageRating)
                .ThenByDescending(x => x.TotalReviews)
                .ThenBy(x => x.ProductId)
                .Take(4)
                .Join(_context.Products.Where(p => p.IsActive),
                    review => review.ProductId, product => product.ProductId,
                    (review, product) => new
                    {
                        product.ProductId,
                        Name = isArabic ? product.NameArb : product.Name,
                        product.Price,
                        review.AverageRating,
                        review.TotalReviews,
                        ImageId = product.ProductImages.OrderBy(img => img.ImageId)
                            .Select(img => (int?)img.ImageId).FirstOrDefault()
                    })
                .ToListAsync();

            var summaries = MarketplaceOffers.ForProducts(_context, topProducts.Select(p => p.ProductId), _configuration);
            return Ok(topProducts.Select(p => new
            {
                p.ProductId, p.Name, p.Price, p.AverageRating, p.TotalReviews,
                lowestSellerPrice = summaries.GetValueOrDefault(p.ProductId)?.LowestPrice,
                availableSellerCount = summaries.GetValueOrDefault(p.ProductId)?.SellerCount ?? 0,
                imageUrl = p.ImageId.HasValue ? Url.Action("Product", "StoreImage", new { id = p.ImageId }) : null
            }));
        }
        [HttpGet]
        public async Task<IActionResult> search([FromQuery] string term, [FromQuery] string lang = "en")
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            if (string.IsNullOrWhiteSpace(term) || term.Trim().Length > 100)
                return Ok(new List<object>());
            term = term.Trim();


            var products = await _context.Products.AsNoTracking()
                .Where(p => p.IsActive && (p.Name.Contains(term) || p.NameArb.Contains(term) ||
                    (p.SKUId != null && p.SKUId.Contains(term)) ||
                    (p.ProductsModel != null && p.ProductsModel.Contains(term)) ||
                    _context.Brand.Any(b => b.BrandId == p.BrandId && (b.BrandNameAEn.Contains(term) || b.BrandNameAr.Contains(term))))
                )
                .OrderBy(p => p.Name)
                .Take(6)
                .Select(p => new
                {
                    productId = p.ProductId,
                    //name = lang == "ar" ? p.NameArb : p.Name,
                    name = isArabic && !string.IsNullOrEmpty(p.NameArb) ? p.NameArb : p.Name,
                    description = isArabic ? p.DescriptionArb : p.Description,
                    //description = lang == "ar" ? p.DescriptionArb : p.Description,
                    price = Math.Round(p.Price, 3),
                    categoryId = p.CategoryId,
                    subCategoryId = p.SubCategoryId,
                    createdAt = p.CreatedAt,

                    TotalStock = _context.ProductStocks
                        .Where(s => s.ProductId == p.ProductId)
                        .Sum(s => (int?)s.StockQty) ?? 0,

                    TotalSoldStock = _context.OrderItems
                        .Where(o => o.ProductId == p.ProductId)
                        .Sum(o => (int?)o.Quantity) ?? 0,

                    Stock =
                        (_context.ProductStocks.Where(s => s.ProductId == p.ProductId)
                            .Sum(s => (int?)s.StockQty) ?? 0)
                        - (_context.OrderItems.Where(o => o.ProductId == p.ProductId)
                            .Sum(o => (int?)o.Quantity) ?? 0),

                    imageBase64 = p.ProductImages.Where(img => img.ImageData != null)
                        .OrderByDescending(img => img.IsPrimary == true).ThenBy(img => img.ImageId)
                        .Select(img => "/StoreImage/Product/" + img.ImageId)
                        .FirstOrDefault(),

                    totalReviews = _context.ProductReviews.Count(r => r.ProductId == p.ProductId),
                    averageRating = _context.ProductReviews
                        .Where(r => r.ProductId == p.ProductId)
                        .Average(r => (double?)r.Rating) ?? 0
                })
                .ToListAsync();

            return Ok(products);


        }

        public IActionResult GetCompanyDetailsJson()
        {
            var company = _context.CompanyDetails.FirstOrDefault();

            if (company == null)
                return Json(null);

            return Json(new
            {
                company.CompanyName,
                company.Address,
                company.Phone,
                company.Email,
                Facebook = company.FacebookUrl,
                Twitter = company.TwitterUrl,
                Instagram = company.InstagramUrl,
                LinkedIn = company.LinkedInUrl,
                Logo = company.Logo != null
                    ? $"data:image/png;base64,{Convert.ToBase64String(company.Logo)}"
                    : "/Images/download.png"
            });
        }


    }
}
