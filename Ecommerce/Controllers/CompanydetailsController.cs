using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Imaging;
namespace EcommerceWebApplication.Controllers
{
    [Authorize]
    public class CompanydetailsController : Controller
    {
        private readonly EcommerceDbContext _context;

        public CompanydetailsController(EcommerceDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            var company = _context.CompanyDetails.FirstOrDefault();
            return View(company ?? new CompanyDetails());
        }
        private static Bitmap ResizeImage(Image image, int maxWidth, int maxHeight)
        {
            var ratioX = (double)maxWidth / image.Width;
            var ratioY = (double)maxHeight / image.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var width = (int)(image.Width * ratio);
            var height = (int)(image.Height * ratio);

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent); // ⭐ IMPORTANT
            graphics.DrawImage(image, 0, 0, width, height);

            return bitmap;
        }

        public static byte[] CompressPng(Image image, int maxWidth, int maxHeight)
        {
            using var resized = ResizeImage(image, maxWidth, maxHeight);
            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png); // ✅ transparency safe
            return ms.ToArray();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CompanyDetails model, IFormFile? LogoFile)
        {
            var existingCompany = await _context.CompanyDetails.FirstOrDefaultAsync();

            if (existingCompany != null)
            {
                
                existingCompany.CompanyName = model.CompanyName;
                existingCompany.Address = model.Address;
                existingCompany.Email = model.Email;
                existingCompany.Phone = model.Phone;
                existingCompany.FacebookUrl = model.FacebookUrl;
                existingCompany.TwitterUrl = model.TwitterUrl;
                existingCompany.InstagramUrl = model.InstagramUrl;
                existingCompany.LinkedInUrl = model.LinkedInUrl;
                existingCompany.Vat = model.Vat;
                existingCompany.DeliveryCharge = model.DeliveryCharge;
                existingCompany.CompanyNameArabic = model.CompanyNameArabic;
                existingCompany.QuantityLimit = model.QuantityLimit;
                existingCompany.AddressArabic = model.AddressArabic;
                existingCompany.UpdatedAt = DateTime.Now;
                existingCompany.UpdatedBy = 1;
                existingCompany.Heading = true;



                /*if (LogoFile != null && LogoFile.Length > 0)
                {
                    using (var stream = LogoFile.OpenReadStream())
                    using (var image = Image.FromStream(stream))
                    {
                        // Use helper method
                        byte[] compressedImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                        existingCompany.Logo = compressedImage;
                    }
                }*/
                if (LogoFile != null && LogoFile.Length > 0)
                {
                    using var stream = LogoFile.OpenReadStream();
                    using var image = Image.FromStream(stream);

                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                    byte[] finalImage;

                    if (hasAlpha && Path.GetExtension(LogoFile.FileName).ToLower() == ".png")
                    {
                        // ✅ Transparent PNG → PNG me hi save
                        finalImage = CompressPng(image, 800, 800);
                    }
                    else
                    {
                        // ✅ JPG / JPEG / non-transparent PNG → original-style compress
                        finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                    }

                    existingCompany.Logo = finalImage;
                }

                _context.CompanyDetails.Update(existingCompany);
                TempData["Success"] = "Company details updated successfully.";
               
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");

        }
        

    }
}
