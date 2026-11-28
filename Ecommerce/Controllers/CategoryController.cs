using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = AdminStaffAccess.Roles)]  // ✅ Only Admin can manage categories
    public class CategoryController : Controller
    {
        private readonly EcommerceDbContext _context;

        private readonly AdminStaffAccess _access;

        public CategoryController(EcommerceDbContext context, AdminStaffAccess access)
        {
            _context = context;
            _access = access;
        }
      
        [HttpGet]
        public async Task<IActionResult> Index(string? name, int? status, int page = 1, int pageSize = 10, bool partial = false)
        {
            var rights = await _access.RightsAsync(User, AdminStaffAccess.Categories, HttpContext.RequestAborted);
            if (!(rights.View)) return Forbid();
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;

            // Start with base query
            var productsQuery = _context.Categories.AsQueryable();

            // Apply Name filter
            if (!string.IsNullOrEmpty(name))
            {
                productsQuery = productsQuery.Where(p =>
                    p.CName.Contains(name) || p.CNameArb.Contains(name));
            }

            // Apply Status filter
            if (status.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.IsActive == (status == 1));
            }

            pageSize = Math.Clamp(pageSize, 1, 100);
            int totalItems = productsQuery.Count();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize)));
            var pagedData = productsQuery.AsNoTracking().OrderByDescending(p => p.CategoryId)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new CategoryListViewModel
                {
                    CategoryId = c.CategoryId,
                
                    CName = isArabic ? c.CNameArb : c.CName,
                    CDescription = isArabic ? c.CDescriptionArbic : c.CDescription,
                  
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    CategoryImageBase64 = c.CategoryImage != null
                        ? "data:image/png;base64," + Convert.ToBase64String(c.CategoryImage)
                        : null,
                    CategoryIconBase64 = c.CategoryIcon != null
                        ? "data:image/png;base64," + Convert.ToBase64String(c.CategoryIcon)
                        : null
                })
                .ToList();
            var pagedModel = new PagedResult<CategoryListViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
            // Pass filters back to view
            ViewBag.FilterName = name;
            ViewBag.FilterStatus = status;

            return partial ? PartialView("_RegisterRows", pagedModel) : View(pagedModel);
        }




        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var rights = await _access.RightsAsync(User, AdminStaffAccess.Categories, HttpContext.RequestAborted);
            if (!(rights.Edit)) return Forbid();
            var category = _context.Categories.FirstOrDefault(c => c.CategoryId == id);
            if (category == null)
                return NotFound();

            return View(category); // ✅ returns full entity for editing
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> Edit(int CategoryId, string CName, string CNameArb, string CDescription, string CDescriptionArbic, bool IsActive, IFormFile? CategoryImage, IFormFile? CategoryIcon)
        {
            var rights = await _access.RightsAsync(User, AdminStaffAccess.Categories, HttpContext.RequestAborted);
            if (!(rights.Edit)) return Forbid();
            if (!int.TryParse(User.FindFirst("UserId")?.Value, out var actor)) return Forbid();
            try
            {
                var category = _context.Categories.FirstOrDefault(c => c.CategoryId == CategoryId);
                if (category == null)
                    return NotFound();

                // ✅ Update basic fields
                category.CName = CName;
                category.CNameArb = CNameArb;
                category.CDescription = CDescription;
                category.CDescriptionArbic = CDescriptionArbic;
                category.IsActive = IsActive;
                category.UpdatedAt = DateTime.Now;
                category.UpdatedBy = actor;

                // ✅ Handle image upload only if a new file is provided

                //if (CategoryImage != null && CategoryImage.Length > 0)
                //{
                //    using (var originalStream = CategoryImage.OpenReadStream())
                //    using (var originalImage = Image.FromStream(originalStream))
                //    {
                //        // ✅ Use your helper method here
                //        var compressedBytes = ImageHelper.CompressImage(originalImage, 800, 800, 70L); // max 800x800, 70% quality
                //        category.CategoryImage = compressedBytes;
                //    }
                //}
                //if (CategoryIcon != null && CategoryIcon.Length > 0)
                //{
                //    using (var originalStream = CategoryIcon.OpenReadStream())
                //    using (var originalImage = Image.FromStream(originalStream))
                //    {
                //        // ✅ Use your helper method here
                //        var compressedBytes = ImageHelper.CompressImage(originalImage, 800, 800, 70L); // max 800x800, 70% quality
                //        category.CategoryIcon = compressedBytes;
                //    }
                //}
                if (CategoryImage != null && CategoryImage.Length > 0)
                {
                    using var stream = CategoryImage.OpenReadStream();
                    using var image = Image.FromStream(stream);

                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                    byte[] finalImage;

                    if (hasAlpha && Path.GetExtension(CategoryImage.FileName).ToLower() == ".png")
                    {
                        // ✅ Transparent PNG
                        finalImage = ImageHelper.CompressPng(image, 800, 800);
                    }
                    else
                    {
                        // ✅ JPG / non-transparent
                        finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                    }

                    category.CategoryImage = finalImage;
                }
               
                if (CategoryIcon != null && CategoryIcon.Length > 0)
                {
                    using var stream = CategoryIcon.OpenReadStream();
                    using var image = Image.FromStream(stream);

                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                    byte[] finalImage;

                    if (hasAlpha && Path.GetExtension(CategoryIcon.FileName).ToLower() == ".png")
                    {
                        finalImage = ImageHelper.CompressPng(image, 800, 800);
                    }
                    else
                    {
                        finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                    }

                    category.CategoryIcon = finalImage;
                }
                // ✅ Save changes to DB
                _context.SaveChanges();

                TempData["Success"] = "✅ Category updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Log the exception properly
                Console.WriteLine($"Error updating category: {ex}");

                TempData["Error"] = "❌ Something went wrong while updating the category. Please try again.";
                return RedirectToAction("Edit", new { CategoryId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var rights = await _access.RightsAsync(User, AdminStaffAccess.Categories, HttpContext.RequestAborted);
            if (!(rights.Delete)) return Forbid();
            try
            {
                var category = _context.Categories.FirstOrDefault(c => c.CategoryId == id);
                if (category == null)
                {
                    return Json(new { success = false, message = "Category not found!" });
                }

                _context.Categories.Remove(category);
                _context.SaveChanges();

                return Json(new { success = true, message = "✅ Category deleted successfully!" });
            }
            catch (Exception ex)
            {
              
                return Json(new { success = false, message = "❌ Can not delete category " });
            }
        }

        // ✅ Show Create Form
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var rights = await _access.RightsAsync(User, AdminStaffAccess.Categories, HttpContext.RequestAborted);
            if (!(rights.Create)) return Forbid();
            return View();
        }

        // ✅ Save Category
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAsync(Category model, IFormFile categoryImage, IFormFile CategoryIcon)
        {
            var rights = await _access.RightsAsync(User, AdminStaffAccess.Categories, HttpContext.RequestAborted);
            if (!(rights.Create)) return Forbid();
            if (!int.TryParse(User.FindFirst("UserId")?.Value, out var actor)) return Forbid();
            if (model.CategoryId != 0) return BadRequest();
            model.SubCategories = null!;
            model.UpdatedBy = null;
            model.UpdatedAt = null;
            try
            {
                //if (categoryImage != null && categoryImage.Length > 0)
                //{
                //    using (var originalStream = categoryImage.OpenReadStream())
                //    using (var originalImage = Image.FromStream(originalStream))
                //    {
                //        // ✅ Use your helper method here
                //        var compressedBytes = ImageHelper.CompressImage(originalImage, 800, 800, 70L); 
                //        model.CategoryImage = compressedBytes;
                //    }
                //}
                //if (CategoryIcon != null && CategoryIcon.Length > 0)
                //{
                //    using (var originalStream = CategoryIcon.OpenReadStream())
                //    using (var originalImage = Image.FromStream(originalStream))
                //    {
                //        // ✅ Use your helper method here
                //        var compressedBytes = ImageHelper.CompressImage(originalImage, 800, 800, 70L); 
                //        model.CategoryIcon = compressedBytes;
                //    }
                //}
                if (categoryImage != null && categoryImage.Length > 0)
                {
                    using var stream = categoryImage.OpenReadStream();
                    using var image = Image.FromStream(stream);

                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                    byte[] finalImage;

                    if (hasAlpha && Path.GetExtension(categoryImage.FileName).ToLower() == ".png")
                    {
                        // ✅ Transparent PNG
                        finalImage = ImageHelper.CompressPng(image, 800, 800);
                    }
                    else
                    {
                        // ✅ JPG / normal image
                        finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                    }

                    model.CategoryImage = finalImage;
                }
                if (CategoryIcon != null && CategoryIcon.Length > 0)
                {
                    using var stream = CategoryIcon.OpenReadStream();
                    using var image = Image.FromStream(stream);

                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                    byte[] finalImage;

                    if (hasAlpha && Path.GetExtension(CategoryIcon.FileName).ToLower() == ".png")
                    {
                        finalImage = ImageHelper.CompressPng(image, 800, 800);
                    }
                    else
                    {
                        finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                    }

                    model.CategoryIcon = finalImage;
                }

                model.CreatedAt = DateTime.Now;
                model.CreatedBy = actor; 

                _context.Categories.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Category added successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating category: {ex.Message}");
                TempData["Error"] = "❌ Something went wrong while creating the category. Please try again.";
                return RedirectToAction("Create");
            }
        }



        ///// <summary>
        ///// Compress and resize an image before saving
        ///// </summary>
        ///// <param name="image">Original image</param>
        ///// <param name="maxWidth">Max width</param>
        ///// <param name="maxHeight">Max height</param>
        ///// <param name="quality">JPEG quality (0-100)</param>
        ///// <returns>Compressed image bytes</returns>
        //private byte[] CompressImage(Image image, int maxWidth, int maxHeight, long quality)
        //{
        //    // ✅ Resize proportionally
        //    int newWidth = image.Width;
        //    int newHeight = image.Height;

        //    if (image.Width > maxWidth || image.Height > maxHeight)
        //    {
        //        double widthRatio = (double)maxWidth / image.Width;
        //        double heightRatio = (double)maxHeight / image.Height;
        //        double ratio = Math.Min(widthRatio, heightRatio);

        //        newWidth = (int)(image.Width * ratio);
        //        newHeight = (int)(image.Height * ratio);
        //    }

        //    using (var resizedBitmap = new Bitmap(image, newWidth, newHeight))
        //    using (var ms = new MemoryStream())
        //    {
        //        // ✅ Get JPEG encoder
        //        var jpegEncoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

        //        // ✅ Set compression quality
        //        var encoderParams = new EncoderParameters(1);
        //        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);

        //        // ✅ Save compressed image
        //        resizedBitmap.Save(ms, jpegEncoder, encoderParams);

        //        return ms.ToArray();
        //    }
        //}


        //Save/Update TopCategory

        [HttpGet]
        public async Task<IActionResult> TopCategory()
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.TopCategories, HttpContext.RequestAborted)).View) return Forbid();
            var categories = _context.Categories.ToList();

            ViewBag.Categories = categories.Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.CName
            }).ToList();

            ViewBag.CategoryImages = categories
                .Where(c => c.CategoryImage != null)
                .ToDictionary(
                    c => c.CategoryId,
                    c => "data:image/png;base64," + Convert.ToBase64String(c.CategoryImage)
                );

            var existingTopCategories = _context.TopCategories
                .OrderBy(tc => tc.TopCategoryId)
                .Take(3)
                .ToList();

            while (existingTopCategories.Count < 3)
            {
                existingTopCategories.Add(new TopCategory());
            }

            return View(existingTopCategories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOrUpdateTopCategory(TopCategory model, IFormFile? TopCategoryImage)
        {
            var rights = await _access.RightsAsync(User, AdminStaffAccess.TopCategories, HttpContext.RequestAborted);
            if (model.TopCategoryId == 0 ? !rights.Create : !rights.Edit) return Forbid();
            if (!int.TryParse(User.FindFirst("UserId")?.Value, out var actor)) return Forbid();
            if (!ModelState.IsValid || model.TopCategoryId < 0 ||
                !await _context.Categories.AnyAsync(c => c.CategoryId == model.CategoryId) ||
                string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.TitleArbic) ||
                string.IsNullOrWhiteSpace(model.SubTitle) || string.IsNullOrWhiteSpace(model.SubTitleArbic) ||
                (model.TopCategoryId == 0 && (TopCategoryImage == null || TopCategoryImage.Length == 0)))
                return BadRequest();
            model.UpdatedBy = null;
            model.UpdatedAt = null;
            if (model.TopCategoryId == 0) model.TopCategoryImage = null;
            if (model.TopCategoryId == 0)
            {

                //if (TopCategoryImage != null && TopCategoryImage.Length > 0)
                //{
                //    using (var originalStream = TopCategoryImage.OpenReadStream())
                //    using (var originalImage = Image.FromStream(originalStream))
                //    {
                //        // ✅ Use your helper method here
                //        var compressedBytes = ImageHelper.CompressImage(originalImage, 800, 800, 70L); // max 800x800, 70% quality
                //        model.TopCategoryImage = compressedBytes;
                //    }
                //}
                if (TopCategoryImage != null && TopCategoryImage.Length > 0)
                {
                    using var stream = TopCategoryImage.OpenReadStream();
                    using var image = Image.FromStream(stream);

                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                    byte[] finalImage;

                    if (hasAlpha && Path.GetExtension(TopCategoryImage.FileName).ToLower() == ".png")
                    {
                        finalImage = ImageHelper.CompressPng(image, 800, 800);
                    }
                    else
                    {
                        finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                    }

                    model.TopCategoryImage = finalImage;
                }
                model.CreatedAt = DateTime.Now;
                model.CreatedBy = actor;

                _context.TopCategories.Add(model);

            }
            else
            {
                var existing = await _context.TopCategories.FindAsync(model.TopCategoryId);
                if (existing == null) return NotFound();

                existing.CategoryId = model.CategoryId;
                existing.Title = model.Title;
                existing.TitleArbic = model.TitleArbic;
                existing.SubTitle = model.SubTitle;
                existing.SubTitleArbic = model.SubTitleArbic;
                existing.UpdatedAt = DateTime.Now;
                existing.UpdatedBy = actor;
                if (TopCategoryImage != null && TopCategoryImage.Length > 0)
                {
                    using (var originalStream = TopCategoryImage.OpenReadStream())
                    using (var originalImage = Image.FromStream(originalStream))
                    {
                        // ✅ Use your helper method here
                        var compressedBytes = ImageHelper.CompressImage(originalImage, 800, 800, 70L); // max 800x800, 70% quality
                        existing.TopCategoryImage = compressedBytes;
                    }
                }
                _context.TopCategories.Update(existing);

            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Top category saved successfully.";
            return RedirectToAction("TopCategory");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTopCategory(int TopCategoryId)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.TopCategories, HttpContext.RequestAborted)).Delete) return Forbid();
            var item = await _context.TopCategories.FindAsync(TopCategoryId);
            if (item != null)
            {
                _context.TopCategories.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Top category deleted successfully.";
            }

            return RedirectToAction("TopCategory");
        }


        [HttpGet]
        public async Task<IActionResult> AutoCompleteSql(string term)
        {
            var rights = await _access.RightsAsync(User, AdminStaffAccess.Categories, HttpContext.RequestAborted);
            if (!(rights.View)) return Forbid();
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            IQueryable<Category> queryable = _context.Categories;

            // Detect if input is Arabic
            bool isArabic = term.Any(c => c >= 0x0600 && c <= 0x06FF);

            if (isArabic)
            {
                queryable = queryable.Where(c => c.CNameArb.Contains(term));
            }
            else
            {
                queryable = queryable.Where(c => c.CName.Contains(term));
            }

            var suggestions = await queryable
                .Select(c => isArabic ? c.CNameArb : c.CName)
                .Take(10)
                .ToListAsync();

            return Json(suggestions);
        }


    }
}
