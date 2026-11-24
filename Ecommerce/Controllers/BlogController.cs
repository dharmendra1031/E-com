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
    [Authorize(Roles = "Admin")]
    public class BlogController : Controller
    {
        private readonly EcommerceDbContext _context;

        public BlogController(EcommerceDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        public IActionResult Add_Blog(int? id)
        {
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
        public async Task<IActionResult> CreateOrUpdate(blogMaster model, IFormFile? BlogImageFile)
        {
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
                    CreatedBy = 1 // Replace with actual logged-in user ID
                };

                if (BlogImageFile != null && BlogImageFile.Length > 0)
                {
                    using var stream = BlogImageFile.OpenReadStream();
                    using var image = Image.FromStream(stream);

                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                    byte[] finalImage;

                    if (hasAlpha && Path.GetExtension(BlogImageFile.FileName).ToLower() == ".png")
                    {
                        // ✅ Transparent PNG
                        finalImage = ImageHelper.CompressPng(image, 800, 800);
                    }
                    else
                    {
                        // ✅ Normal image
                        finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                    }

                    newBlog.BlogImage = finalImage;
                }

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
                existingBlog.UpdatedBy = 1; // Replace with actual logged-in user ID

                if (BlogImageFile != null && BlogImageFile.Length > 0)
                {
                    using var stream = BlogImageFile.OpenReadStream();
                    using var image = Image.FromStream(stream);

                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                    byte[] finalImage;

                    if (hasAlpha && Path.GetExtension(BlogImageFile.FileName).ToLower() == ".png")
                    {
                        finalImage = ImageHelper.CompressPng(image, 800, 800);
                    }
                    else
                    {
                        finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                    }

                    existingBlog.BlogImage = finalImage;
                }

                _context.blogmaster.Update(existingBlog);

            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Blog saved successfully.";
            return RedirectToAction("BlogList");
        }
       
        [HttpGet]
        public IActionResult BlogList(int page = 1, int pageSize = 10)
        {
            var blogList = _context.blogmaster
                .OrderByDescending(b => b.BlogId)
                .ToList() // Materialize query first
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
            int totalItems = blogList.Count;
            var pagedData = blogList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
        public IActionResult Delete(int id)
        {
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
