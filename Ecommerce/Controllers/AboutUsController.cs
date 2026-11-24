using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.Drawing.Imaging;

namespace EcommerceWebApplication.Controllers
{
    [Authorize]
    public class AboutUsController : Controller
    {
        private readonly EcommerceDbContext _context;

        public AboutUsController(EcommerceDbContext context)
        {
            _context = context;
        }

        
        public IActionResult UpdateAbout()
        {
            var about = _context.AboutUs.FirstOrDefault();
            return View(about ?? new AboutUs());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(AboutUs model, IFormFile? AboutImageFile)
        {
            if (!ModelState.IsValid)
            {
                return View("UpdateAbout", model);
            }

            var existingAbout = _context.AboutUs.FirstOrDefault();

            // 🔹 IMAGE PROCESS (common)
            byte[]? compressedImage = null;
            if (AboutImageFile != null && AboutImageFile.Length > 0)
            {
                using (var stream = AboutImageFile.OpenReadStream())
                using (var image = Image.FromStream(stream))
                {
                    compressedImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                }
            }

            if (existingAbout != null)
            {
                // 🔄 UPDATE
                existingAbout.TitleEng = model.TitleEng;
                existingAbout.SubTitleArb = model.SubTitleArb;
                existingAbout.TitleArb = model.TitleArb;
                existingAbout.SubTitleEng = model.SubTitleEng;
                existingAbout.DescriptionEng = model.DescriptionEng;
                existingAbout.DescriptionArb = model.DescriptionArb;
                existingAbout.IsActive = model.IsActive;

                if (compressedImage != null)
                    existingAbout.AboutImage = compressedImage;

                existingAbout.UpdatedAt = DateTime.Now;
                existingAbout.UpdatedBy = 1; // 🔐 AdminUserId lagao agar session me ho

                _context.AboutUs.Update(existingAbout);
                TempData["Success"] = "About Us updated successfully.";
            }
            else
            {
                // ➕ CREATE
                model.AboutImage = compressedImage;
                model.CreatedAt = DateTime.Now;
                model.CreatedBy = 1; // 🔐 AdminUserId
                //model.IsActive = true;

                _context.AboutUs.Add(model);
                TempData["Success"] = "About Us created successfully.";
            }

            _context.SaveChanges();
            return RedirectToAction("UpdateAbout");
        }

    }
}
