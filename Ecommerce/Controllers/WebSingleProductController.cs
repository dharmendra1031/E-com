using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace EcommerceWebApplication.Controllers
{
    public class WebSingleProductController : Controller
    {
        private readonly EcommerceDbContext _context;

        public WebSingleProductController(EcommerceDbContext context)
        {
            _context = context;
        }

        public IActionResult SingleProduct(int id)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            var productEntity = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.ProductReviews)
                    .ThenInclude(r => r.User)
                .Include(p => p.ProductImages)
                .FirstOrDefault(p => p.ProductId == id);

            if (productEntity == null)
                return NotFound();

            var specifications = _context.Specification
       .Where(s => s.ProductId == id)
       .Select(s => new SingleSpecificationDto
       {
           Id = s.Id,
           SpeciNameAr = s.SpeciNameAr != null ? s.SpeciNameAr : "",
           SpecivalueAr = s.SpecivalueAr != null ? s.SpecivalueAr : "",
           SpeciNameEn = s.SpeciNameEn != null ? s.SpeciNameEn : "",
           SpecivalueEn = s.SpecivalueEn != null ? s.SpecivalueEn : ""

       }).ToList();

            var weekendDeal = (from wd in _context.WekendDeals
                               join p in _context.Products
                                   on wd.ProductId equals p.ProductId
                               where p.ProductId == id
                                     && wd.FromDate <= DateTime.Now
                                     && wd.ToDate >= DateTime.Now
                               select new
                               {
                                   wd.DiscPer,
                                   OfferPrice = p.Price - ((p.Price * wd.DiscPer) / 100),
                                   DiscountAmount = (p.Price * wd.DiscPer) / 100
                               }).FirstOrDefault();

            var product = new SingleProductVM
            {
                ProductId = productEntity.ProductId,
                //Name = productEntity.Name,
                //NameArb = productEntity.NameArb,
                Name = isArabic ? productEntity.NameArb : productEntity.Name,
                Description = isArabic ? productEntity.DescriptionArb : productEntity.Description,
                //Description = productEntity.Description,
                //DescriptionArb = productEntity.DescriptionArb,
                Brand = productEntity.Brand != null
                    ? new SingleBrandDto
                    {
                        BrandId = productEntity.Brand.BrandId,
                        //BrandName = productEntity.Brand.BrandNameAEn,
                        BrandName = isArabic ? productEntity.Brand.BrandNameAr : productEntity.Brand.BrandNameAEn,
                        BrandNameAr = productEntity.Brand.BrandNameAr,
                        BrandLogoBase64 = productEntity.Brand.BrandLogo != null
                            ? $"data:image/png;base64,{Convert.ToBase64String(productEntity.Brand.BrandLogo)}"
                            : "/images/default-brand.png"
                    }
                    : null,
                SKUId = productEntity.SKUId,
                Units = productEntity.Units ?? 1,
                ProductsModel = productEntity.ProductsModel,
                Warranty = productEntity.Warranty,
                Price = productEntity.Price,
                Stock = productEntity.Stock,
                Weight = productEntity.Weight ?? 0,
                AverageRating = productEntity.ProductReviews.Any() ? productEntity.ProductReviews.Average(r => r.Rating) : 0,
                TotalReviews = productEntity.ProductReviews.Count(),
                Reviews = productEntity.ProductReviews.Select(r => new SingleReviewDto
                {
                    ReviewId = r.ReviewId,
                    ProductId = r.ProductId,
                    UserId = r.UserId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                }).ToList(),



                Images = productEntity.ProductImages.Select(img => new SigleImageDto
                {
                    ImageId = img.ImageId,
                    ProductId = productEntity.ProductId,
                    IsPrimary = img.IsPrimary ?? false,
                    Base64 = img.ImageData != null
                        ? $"data:image/png;base64,{Convert.ToBase64String(img.ImageData)}"
                        : "/images/default-product.png"
                }).ToList(),
                Specifications = specifications // ✅ yaha add kiya


            };
            // ✅ Weekend Deal mapping (ADD ONLY – kuch bhi hataya nahi)
            if (weekendDeal != null)
            {
                product.IsWeekendDeal = true;
                product.DiscPer = weekendDeal.DiscPer;
                product.OfferPrice = weekendDeal.OfferPrice;
                product.DiscountAmount = weekendDeal.DiscountAmount;
            }
            else
            {
                product.IsWeekendDeal = false;
            }
          
            return View(product);
        }
    }
}
