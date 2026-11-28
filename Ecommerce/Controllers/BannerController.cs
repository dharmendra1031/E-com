using System.Data;
using System.Drawing;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers;

[Authorize(Roles = AdminStaffAccess.Roles)]
public class BannerController(EcommerceDbContext context, AdminStaffAccess access) : Controller
{
    [HttpGet]
    public async Task<IActionResult> AddBanner(int? id)
    {
        var rights = await access.RightsAsync(User, AdminStaffAccess.Banners, HttpContext.RequestAborted);
        if (!rights.View || (id.HasValue && !rights.Edit)) return Forbid();
        var model = id.HasValue ? await context.Banner.AsNoTracking().FirstOrDefaultAsync(b => b.BannerId == id) : new BannerModel();
        if (model == null) return NotFound();
        var banners = await context.Banner.AsNoTracking().OrderBy(b => b.DisplayOrder).ThenBy(b => b.BannerId).ToListAsync();
        ViewBag.BannerCount = banners.Count;
        ViewBag.BannerList = banners;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(12_000_000)]
    public async Task<IActionResult> AddBanner(BannerModel model, IFormFile? BannerFile)
    {
        var rights = await access.RightsAsync(User, AdminStaffAccess.Banners, HttpContext.RequestAborted);
        if (model.BannerId == 0 ? !rights.Create : !rights.Edit) return Forbid();
        if (!ModelState.IsValid || model.BannerId < 0 || string.IsNullOrWhiteSpace(model.Title) ||
            string.IsNullOrWhiteSpace(model.TitleArabic) || model.DisplayOrder < 0 ||
            (model.BannerId == 0 && (BannerFile == null || BannerFile.Length == 0)) || BannerFile?.Length > 10_000_000)
            return BadRequest("Invalid banner details.");

        byte[]? bytes = null;
        if (BannerFile is { Length: > 0 })
        {
            try
            {
                using var stream = BannerFile.OpenReadStream();
                using var image = Image.FromStream(stream);
                if (image.Width > 8000 || image.Height > 8000 || (long)image.Width * image.Height > 25_000_000)
                    return BadRequest("Image dimensions are too large.");
                bytes = Image.IsAlphaPixelFormat(image.PixelFormat) && Path.GetExtension(BannerFile.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase)
                    ? ImageHelper.CompressPng(image, 800, 800) : ImageHelper.CompressImage(image, 800, 800, 70L);
            }
            catch (ArgumentException) { return BadRequest("Invalid image."); }
        }
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var banner = model.BannerId == 0 ? new BannerModel() : await context.Banner.FindAsync(model.BannerId);
        if (banner == null) return NotFound();
        if (model.BannerId == 0)
        {
            if (await context.Banner.CountAsync() >= 3)
            {
                TempData["Error"] = "Maximum 3 banners allowed.";
                return RedirectToAction(nameof(AddBanner));
            }
            context.Banner.Add(banner);
        }
        banner.Title = model.Title.Trim();
        banner.TitleArabic = model.TitleArabic.Trim();
        banner.DisplayOrder = model.DisplayOrder ?? banner.DisplayOrder;
        if (bytes != null) banner.BannerImage = bytes;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Banner saved successfully.";
        return RedirectToAction(nameof(AddBanner));
    }

    [HttpGet]
    public async Task<IActionResult> EditBanner(int id)
    {
        if (!(await access.RightsAsync(User, AdminStaffAccess.Banners, HttpContext.RequestAborted)).Edit) return Forbid();
        var banner = await context.Banner.AsNoTracking().FirstOrDefaultAsync(b => b.BannerId == id);
        return banner == null ? NotFound() : View(banner);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(12_000_000)]
    public Task<IActionResult> EditBanner(BannerModel model, IFormFile? BannerFile) =>
        model.BannerId > 0 ? AddBanner(model, BannerFile) : Task.FromResult<IActionResult>(BadRequest());
}
