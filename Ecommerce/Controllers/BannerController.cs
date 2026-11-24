using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Drawing;

namespace EcommerceWebApplication.Controllers
{
    [Authorize]
    public class BannerController : Controller
    {
        private readonly EcommerceDbContext _context;

        public BannerController(EcommerceDbContext context)
        {
            _context = context;
        }
      
        [HttpGet]
        public IActionResult AddBanner(int? id)
        {
            BannerModel? model = new BannerModel();

            if (id != null)
            {
                model = _context.Banner.FirstOrDefault(x => x.BannerId == id);
                if (model == null)
                    return NotFound();
            }
            ViewBag.BannerCount = _context.Banner.Count();
            ViewBag.BannerList = _context.Banner.OrderBy(x => x.DisplayOrder).ToList();
            return View(model);
        }





        [HttpPost]
        public async Task<IActionResult> AddBanner(BannerModel model, IFormFile BannerFile)
        {
            if (model.BannerId == 0)
            {
                // ADD NEW BANNER
                BannerModel newBanner = new BannerModel
                {
                    Title = model.Title,
                    TitleArabic = model.TitleArabic,
                    DisplayOrder = model.DisplayOrder
                };

                if (BannerFile != null)
                {
                    using var stream = BannerFile.OpenReadStream();
                    using var image = Image.FromStream(stream);

                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                    byte[] finalImage;

                    if (hasAlpha && Path.GetExtension(BannerFile.FileName).ToLower() == ".png")
                    {
                        // ✅ Transparent PNG
                        finalImage = ImageHelper.CompressPng(image, 800, 800);
                    }
                    else
                    {
                        // ✅ Normal image
                        finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                    }

                    newBanner.BannerImage = finalImage;
                }

                _context.Banner.Add(newBanner);

            }
            else
            {
                // UPDATE BANNER
                var banner = _context.Banner.FirstOrDefault(x => x.BannerId == model.BannerId);
                if (banner == null)
                {
                    TempData["Error"] = "Banner not found.";
                    return RedirectToAction("AddBanner");
                }

                banner.Title = model.Title;
                banner.TitleArabic = model.TitleArabic;
                banner.DisplayOrder = model.DisplayOrder;

                if (BannerFile != null)
                {
                    using var stream = BannerFile.OpenReadStream();
                    using var image = Image.FromStream(stream);

                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                    byte[] finalImage;

                    if (hasAlpha && Path.GetExtension(BannerFile.FileName).ToLower() == ".png")
                    {
                        finalImage = ImageHelper.CompressPng(image, 800, 800);
                    }
                    else
                    {
                        finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                    }

                    banner.BannerImage = finalImage;
                }


            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Banner saved successfully.";
            return RedirectToAction("AddBanner");
        }

        [HttpGet]
        public IActionResult EditBanner(int id)
        {
            var banner = _context.Banner.FirstOrDefault(x => x.BannerId == id);
            if (banner == null)
                return NotFound();

            return View(banner);
        }

        [HttpPost]
        public async Task<IActionResult> EditBanner(BannerModel model, IFormFile BannerFile)
        {
            var banner = _context.Banner.FirstOrDefault(x => x.BannerId == model.BannerId);
            if (banner == null)
                return NotFound();

            banner.Title = model.Title;
            banner.TitleArabic = model.TitleArabic;
            banner.DisplayOrder = model.DisplayOrder;

           
            if (BannerFile != null && BannerFile.Length > 0)
            {
                using var stream = BannerFile.OpenReadStream();
                using var image = Image.FromStream(stream);

                bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                byte[] finalImage;

                if (hasAlpha && Path.GetExtension(BannerFile.FileName).ToLower() == ".png")
                {
                    finalImage = ImageHelper.CompressPng(image, 800, 800);
                }
                else
                {
                    finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                }

                banner.BannerImage = finalImage;
            }

            _context.Update(banner);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Banner updated successfully!";
            return RedirectToAction("AddBanner");
        }

    }
}
