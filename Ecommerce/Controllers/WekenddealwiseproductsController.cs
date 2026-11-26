using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceWebApplication.Controllers
{
    public class WekenddealwiseproductsController : Controller
    {
        private readonly EcommerceDbContext _context;

        private readonly IConfiguration _configuration;
        public WekenddealwiseproductsController(EcommerceDbContext context, IConfiguration configuration)
        { _context = context; _configuration = configuration; }
        public IActionResult Index()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;

            var weekendDeals = (from wd in _context.WekendDeals
                                join p in _context.Products
                                    on wd.ProductId equals p.ProductId
                                where wd.FromDate <= DateTime.Now
                          && wd.ToDate >= DateTime.Now && p.IsActive
                                select new WeekendDealProductVM
                                {
                                    DealId = wd.WdId,
                                    //WdNameEn = wd.WdNameEn,
                                    //WdNameAr = wd.WdNameAr,
                                    WdNameEn = isArabic ? wd.WdNameAr : wd.WdNameEn,
                                    DiscPer = wd.DiscPer,
                                    FromDate = wd.FromDate,
                                    ToDate = wd.ToDate,

                                    ProductId = p.ProductId,
                                    //ProductName = p.Name,
                                    //ProductNameAr = p.NameArb,
                                    ProductName = isArabic ? p.NameArb : p.Name,
                                    ProductPrice = p.Price,
                                    Stock = (_context.ProductStocks.Where(st => st.ProductId == p.ProductId).Sum(st => (int?)st.StockQty) ?? 0)
                                        - (_context.OrderItems.Where(o => o.ProductId == p.ProductId).Sum(o => (int?)o.Quantity) ?? 0),

                                    DiscountAmount = (p.Price * wd.DiscPer) / 100,
                                    OfferPrice = p.Price - ((p.Price * wd.DiscPer) / 100),

                                    ProductImagesBase64 = p.ProductImages
                                        .OrderBy(img => img.ImageId).Take(1)
                                        .Select(img => "/StoreImage/Product/" + img.ImageId)
                                        .ToList()
                                })
                                .OrderByDescending(x => x.DealId)
                                
                                .ToList();

            ViewBag.SellerSummaries = MarketplaceOffers.ForProducts(_context, weekendDeals.Select(d => d.ProductId), _configuration);
            return View(weekendDeals);
        }

    }
}
