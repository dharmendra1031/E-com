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
    [Authorize(Roles = AdminStaffAccess.Roles)]
    public class AboutUsController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly AdminStaffAccess _access;

        public AboutUsController(EcommerceDbContext context, AdminStaffAccess access)
        {
            _context = context;
            _access = access;
        }

        
        [HttpGet]
        public async Task<IActionResult> UpdateAbout()
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.About, HttpContext.RequestAborted)).View) return Forbid();
            var about = _context.AboutUs.OrderBy(x=>x.AboutUsId).FirstOrDefault();
            return View(about ?? new AboutUs());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> Create(AboutUs model, IFormFile? AboutImageFile)
        {
            var rights=await _access.RightsAsync(User,AdminStaffAccess.About,HttpContext.RequestAborted);
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var existingAbout = await _context.AboutUs.OrderBy(x=>x.AboutUsId).FirstOrDefaultAsync();
            if (existingAbout==null ? !rights.Create : !rights.Edit) return Forbid();
            if(!int.TryParse(User.FindFirst("UserId")?.Value,out var actor) || actor<=0 || AboutImageFile?.Length>10_000_000) return BadRequest();
            if (!ModelState.IsValid)
            {
                model.AboutUsId=existingAbout?.AboutUsId??0;
                model.AboutImage=existingAbout?.AboutImage;
                return View("UpdateAbout", model);
            }



            // 🔹 IMAGE PROCESS (common)
            byte[]? compressedImage = null;
            if (AboutImageFile != null && AboutImageFile.Length > 0)
            {
                using (var stream = AboutImageFile.OpenReadStream())
                using (var image = Image.FromStream(stream))
                {
                    if (image.Width>8000 || image.Height>8000 || (long)image.Width*image.Height>25_000_000) return BadRequest();
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
                existingAbout.UpdatedBy = actor;

                _context.AboutUs.Update(existingAbout);

            }
            else
            {
                // ➕ CREATE
                model.AboutImage = compressedImage;
                model.CreatedAt = DateTime.Now;
                model.AboutUsId=0;
                model.UpdatedBy=null;
                model.UpdatedAt=null;
                model.CreatedBy = actor;
                //model.IsActive = true;

                _context.AboutUs.Add(model);

            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = "About Us saved successfully.";
            return RedirectToAction("UpdateAbout");
        }

    }
}
