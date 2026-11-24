using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
    public class WebSingleProductController : Controller
    {
        private const int ReviewPageSize = 10;
        private readonly EcommerceDbContext _context;
        public WebSingleProductController(EcommerceDbContext context) => _context = context;

        public IActionResult SingleProduct(int id)
        {
            bool ar = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var product = _context.Products.AsNoTracking().Where(p => p.ProductId == id && p.IsActive)
                .Select(p => new SingleProductVM
                {
                    ProductId = p.ProductId,
                    Name = ar && !string.IsNullOrEmpty(p.NameArb) ? p.NameArb : p.Name,
                    Description = ar && !string.IsNullOrEmpty(p.DescriptionArb) ? p.DescriptionArb : p.Description,
                    CategoryId = p.CategoryId,
                    SubCategoryId = p.SubCategoryId,
                    CategoryName = ar ? p.Category.CNameArb : p.Category.CName,
                    SubCategoryName = ar ? p.SubCategory.SCNameArb : p.SubCategory.SCName,
                    BrandId = p.BrandId,
                    Brand = p.Brand == null ? null : new SingleBrandDto
                    {
                        BrandId = p.Brand.BrandId,
                        BrandName = ar ? p.Brand.BrandNameAr : p.Brand.BrandNameAEn,
                        BrandLogoBase64 = p.Brand.BrandLogo != null ? "/StoreImage/Brand/" + p.Brand.BrandId : null
                    },
                    SKUId = p.SKUId,
                    Ds_ProdID = p.Ds_ProdID,
                    Units = p.Units ?? 1,
                    ProductsModel = p.ProductsModel,
                    Warranty = p.Warranty,
                    Price = p.Price,
                    Stock = (_context.ProductStocks.Where(s => s.ProductId == p.ProductId).Sum(s => (int?)s.StockQty) ?? 0)
                          - (_context.OrderItems.Where(o => o.ProductId == p.ProductId).Sum(o => (int?)o.Quantity) ?? 0),
                    Weight = p.Weight ?? 0,
                    AverageRating = p.ProductReviews.Average(r => (double?)r.Rating) ?? 0,
                    TotalReviews = p.ProductReviews.Count()
                }).FirstOrDefault();
            if (product == null) return NotFound();

            product.Images = _context.ProductImages.AsNoTracking()
                .Where(i => i.ProductId == id && i.ImageData != null)
                .OrderByDescending(i => i.IsPrimary == true).ThenBy(i => i.ImageId)
                .Select(i => new SigleImageDto
                {
                    ImageId = i.ImageId, ProductId = i.ProductId, IsPrimary = i.IsPrimary ?? false,
                    Base64 = "/StoreImage/Product/" + i.ImageId
                }).ToList();
            product.Specifications = _context.Specification.AsNoTracking().Where(s => s.ProductId == id)
                .OrderBy(s => s.Id).Select(s => new SingleSpecificationDto
                {
                    Id = s.Id, SpeciNameAr = s.SpeciNameAr ?? "", SpecivalueAr = s.SpecivalueAr ?? "",
                    SpeciNameEn = s.SpeciNameEn ?? "", SpecivalueEn = s.SpecivalueEn ?? ""
                }).ToList();
            product.Reviews = _context.ProductReviews.AsNoTracking().Where(r => r.ProductId == id)
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReviewId).Take(ReviewPageSize)
                .Select(r => new SingleReviewDto
                {
                    ReviewId = r.ReviewId, ProductId = r.ProductId, Rating = r.Rating,
                    Comment = r.Comment, CreatedAt = r.CreatedAt
                }).ToList();
            var now = DateTime.Now;
            var deal = _context.WekendDeals.AsNoTracking()
                .Where(d => d.ProductId == id && d.FromDate <= now && d.ToDate >= now)
                .OrderByDescending(d => d.WdId).FirstOrDefault();
            if (deal != null)
            {
                product.IsWeekendDeal = true;
                product.DiscPer = deal.DiscPer;
                product.DiscountAmount = product.Price * deal.DiscPer / 100;
                product.OfferPrice = product.Price - product.DiscountAmount;
            }
            return View(product);
        }

        [HttpGet]
        public IActionResult Reviews(int id, int page = 1)
        {
            if (page < 1 || page > int.MaxValue / ReviewPageSize) return BadRequest();
            var rows = _context.ProductReviews.AsNoTracking()
                .Where(r => r.ProductId == id && r.Product.IsActive)
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReviewId)
                .Skip((page - 1) * ReviewPageSize).Take(ReviewPageSize + 1)
                .Select(r => new { r.Rating, r.Comment, r.CreatedAt }).ToList();
            return Ok(new { items = rows.Take(ReviewPageSize), hasMore = rows.Count > ReviewPageSize });
        }
    }
}
