using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Imaging;
namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CompanydetailsController : Controller
    {
        private readonly EcommerceDbContext _context;

        public CompanydetailsController(EcommerceDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            var company = _context.CompanyDetails.AsNoTracking().OrderBy(c => c.CompanyId).FirstOrDefault();
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
            var existingCompany = await _context.CompanyDetails.OrderBy(c => c.CompanyId).FirstOrDefaultAsync();
            if (existingCompany == null)
            {
                TempData["Error"] = "Company profile not found.";
                return RedirectToAction("Index");
            }

            var ar = System.Globalization.CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(model.Email) ||
                string.IsNullOrWhiteSpace(model.Email))
                ModelState.AddModelError(nameof(model.Email), ar ? "البريد الإلكتروني غير صالح." : "Enter a valid email address.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(model.Phone ?? "", @"\A\+965[0-9]{8}\z"))
                ModelState.AddModelError(nameof(model.Phone), ar ? "أدخل +965 ثم ثمانية أرقام." : "Enter +965 followed by eight digits.");

            // Redisplay submitted values without replacing the saved logo on validation failure.
            model.CompanyId = existingCompany.CompanyId;
            model.Logo = existingCompany.Logo;
            if (!ModelState.IsValid) return View("Index", model);

            if (LogoFile is { Length: > 0 })
            {
                try
                {
                    using var stream = LogoFile.OpenReadStream();
                    using var image = Image.FromStream(stream);
                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);
                    model.Logo = hasAlpha && Path.GetExtension(LogoFile.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase)
                        ? CompressPng(image, 800, 800)
                        : ImageHelper.CompressImage(image, 800, 800, 70L);
                }
                catch (ArgumentException)
                {
                    ModelState.AddModelError("LogoFile", ar ? "ملف الصورة غير صالح." : "Invalid image file.");
                    return View("Index", model);
                }
            }

            existingCompany.CompanyName = model.CompanyName;
            existingCompany.CompanyNameArabic = model.CompanyNameArabic;
            existingCompany.Address = model.Address;
            existingCompany.AddressArabic = model.AddressArabic;
            existingCompany.Email = model.Email;
            existingCompany.Phone = model.Phone;
            existingCompany.FacebookUrl = model.FacebookUrl;
            existingCompany.TwitterUrl = model.TwitterUrl;
            existingCompany.InstagramUrl = model.InstagramUrl;
            existingCompany.LinkedInUrl = model.LinkedInUrl;
            existingCompany.Vat = model.Vat;
            existingCompany.DeliveryCharge = model.DeliveryCharge;
            existingCompany.QuantityLimit = model.QuantityLimit;
            existingCompany.Logo = model.Logo;
            existingCompany.UpdatedAt = DateTime.Now;
            existingCompany.UpdatedBy = 1;
            existingCompany.Heading = true;

            await _context.SaveChangesAsync();
            TempData["Success"] = ar ? "تم تحديث بيانات الشركة بنجاح." : "Company details updated successfully.";
            return RedirectToAction("Index");
        }
    }
}
