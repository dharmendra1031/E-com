using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing;


namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = AdminStaffAccess.Roles)]
    public class BlogController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly AdminStaffAccess _access;

        public BlogController(EcommerceDbContext context, AdminStaffAccess access)
        {
            _context = context;
            _access = access;
        }
        [HttpGet]
        public async Task<IActionResult> Add_Blog(int? id)
        {
            var rights = await _access.RightsAsync(User, AdminStaffAccess.Blog, HttpContext.RequestAborted);
            if (id.HasValue ? !rights.Edit : !rights.Create) return Forbid();
            if (id == null) // Create mode
            {
                return View(new blogMaster()); // Empty model to bind
            }

            var blog = _context.blogmaster.FirstOrDefault(c => c.BlogId == id);
            if (blog == null)
                return NotFound();

            return View(blog); // Edit mode
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> CreateOrUpdate(blogMaster model, IFormFile? BlogImageFile)
        {
            var rights = await _access.RightsAsync(User, AdminStaffAccess.Blog, HttpContext.RequestAborted);
            if (model.BlogId == 0 ? !rights.Create : !rights.Edit) return Forbid();
            if (model.BlogId < 0 || !int.TryParse(User.FindFirst("UserId")?.Value,out var actor) || actor <= 0 || BlogImageFile?.Length > 10_000_000)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the errors in the form.";
                return View("Add_Blog", model);
            }

            var existingBlog = await _context.blogmaster.FirstOrDefaultAsync(b => b.BlogId == model.BlogId);

            if (model.BlogId > 0 && existingBlog == null)
            {
                TempData["Error"] = "Blog not found.";
                return RedirectToAction("BlogList");
            }
            byte[]? finalImage = null;
            if (BlogImageFile is { Length: > 0 })
            {
                try
                {
                    using var stream = BlogImageFile.OpenReadStream();
                    using var image = Image.FromStream(stream);
                    if (image.Width > 8000 || image.Height > 8000 || (long)image.Width * image.Height > 25_000_000)
                        return BadRequest("Image dimensions are too large.");
                    finalImage = Image.IsAlphaPixelFormat(image.PixelFormat) && Path.GetExtension(BlogImageFile.FileName).Equals(".png",StringComparison.OrdinalIgnoreCase)
                        ? ImageHelper.CompressPng(image,800,800) : ImageHelper.CompressImage(image,800,800,70L);
                }
                catch (ArgumentException) { return BadRequest("Invalid image."); }
            }
            if (existingBlog == null)
            {
                // New blog
                var newBlog = new blogMaster
                {
                    Title = model.Title?.Trim(),
                    TitleArbic = model.TitleArbic?.Trim(),
                    DescriptionEng = model.DescriptionEng?.Trim(),
                    DescriptionArb = model.DescriptionArb?.Trim(),
                    IsActive = model.IsActive,
                    CreatedAt = DateTime.Now,
                    CreatedBy = actor
                };

                newBlog.BlogImage = finalImage;

                _context.blogmaster.Add(newBlog);

            }
            else
            {
                // Update existing blog
                existingBlog.Title = model.Title?.Trim();
                existingBlog.TitleArbic = model.TitleArbic?.Trim();
                existingBlog.DescriptionEng = model.DescriptionEng?.Trim();
                existingBlog.DescriptionArb = model.DescriptionArb?.Trim();
                existingBlog.IsActive = model.IsActive;
                existingBlog.UpdatedAt = DateTime.Now;
                existingBlog.UpdatedBy = actor;

                if (finalImage != null) existingBlog.BlogImage = finalImage;


            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Blog saved successfully.";
            return RedirectToAction("BlogList");
        }
       
        [HttpGet]
        public async Task<IActionResult> BlogList(int page = 1, int pageSize = 10)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Blog, HttpContext.RequestAborted)).View) return Forbid();
            pageSize = Math.Clamp(pageSize,1,100);
            int totalItems = await _context.blogmaster.CountAsync();
            page = Math.Clamp(page,1,Math.Max(1,(int)Math.Ceiling((double)totalItems/pageSize)));
            var pagedData = (await _context.blogmaster.AsNoTracking()
                .OrderByDescending(b => b.BlogId).Skip((page-1)*pageSize).Take(pageSize)
                .ToListAsync())
                .Select(b => new BlogViewModel
                {
                    BlogId = b.BlogId,
                    Title = b.Title ?? string.Empty,
                    DescriptionEng = b.DescriptionEng ?? string.Empty,
                    IsActive = b.IsActive,
                    CreatedAt = b.CreatedAt,
                    BlogImageBase64 = (b.BlogImage != null && b.BlogImage.Length > 0)
                        ? "data:image/png;base64," + Convert.ToBase64String(b.BlogImage)
                        : null
                })

                .ToList();


            var pagedModel = new PagedResult<BlogViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
            return View(pagedModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Blog, HttpContext.RequestAborted)).Delete) return Forbid();
            try
            {
                var blog = _context.blogmaster.FirstOrDefault(c => c.BlogId == id);
                if (blog == null)
                {
                    return Json(new { success = false, message = "blog not found!" });
                }

                _context.blogmaster.Remove(blog);
                _context.SaveChanges();

                return Json(new { success = true, message = "✅ blog deleted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Can not delete blog " });
            }

        }
    
    }
}
