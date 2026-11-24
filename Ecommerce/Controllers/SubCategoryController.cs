using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Drawing;
using System.Drawing.Imaging;

namespace EcommerceWebApplication.Controllers
{
    [Authorize]
    public class SubCategoryController : Controller
    {
        private readonly EcommerceDbContext _context;

        public SubCategoryController(EcommerceDbContext context)
        {
            _context = context;
        }

        public IActionResult CreateSubCategory()
        {
            // Load Parent Categories for dropdown
            ViewBag.Categories = new SelectList(_context.Categories.Where(c => c.IsActive == true), "CategoryId", "CName");
            return View();
        }
        [HttpGet]
        public IActionResult List(string? name, int? status, int? categoryId, int page = 1, int pageSize = 10)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            var query = _context.SubCategories
                .Include(sc => sc.ParentCategory)
                .AsQueryable();

            bool isFilterApplied =
                !string.IsNullOrWhiteSpace(name) ||
                status.HasValue ||
                categoryId.HasValue;

            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(sc => sc.SCName.Contains(name) || sc.SCNameArb.Contains(name));

            if (status.HasValue)
                query = query.Where(sc => sc.IsActive == (status.Value == 1));

            if (categoryId.HasValue)
                query = query.Where(sc => sc.ParentCategoryId == categoryId);

            if (!isFilterApplied)
                query = query.OrderByDescending(p => p.SubCategoryId);

            var subCategories = query.Select(sc => new SubCategoryListViewModel
            {
                SubCategoryId = sc.SubCategoryId,
                ParentCategoryId = sc.ParentCategoryId,
                ParentCategoryName =  sc.ParentCategory != null ? sc.ParentCategory.CName : "N/A",
                SCName = isArabic ? sc.SCNameArb : sc.SCName,
                SCDescription = isArabic ? sc.SCDescriptionArb : sc.SCDescription,
                SubCategoryImageBase64 = sc.SubCategoryImage != null
                    ? "data:image/jpeg;base64," + Convert.ToBase64String(sc.SubCategoryImage)
                    : null,
                IsActive = sc.IsActive,
                CreatedAt = sc.CreatedAt
            }).ToList();

            // 🔹 Pagination
            int totalItems = subCategories.Count;
            var pagedData = subCategories
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedModel = new PagedResult<SubCategoryListViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            // Filters ke liye (agar view me chahiye)
            ViewBag.FilterName = name;
            ViewBag.FilterStatus = status;
            ViewBag.FilterCategoryId = categoryId;

            ViewBag.Categories = new SelectList(
                _context.Categories.Where(c => c.IsActive),
                "CategoryId",
                "CName"
            );

            return View(pagedModel);
        }


        //[HttpGet]
        //public IActionResult List(string? name, int? status, int? categoryId)
        //{
        //    var query = _context.SubCategories
        //        .Include(sc => sc.ParentCategory)
        //        .AsQueryable();


        //    bool isFilterApplied =
        //        !string.IsNullOrWhiteSpace(name) ||
        //        status.HasValue ||
        //        categoryId.HasValue;


        //    if (!string.IsNullOrWhiteSpace(name))
        //    {
        //        query = query.Where(sc => sc.SCName.Contains(name)|| sc.SCNameArb.Contains(name));
        //    }

        //    if (status.HasValue)
        //    {
        //        bool isActive = status.Value == 1;
        //        query = query.Where(sc => sc.IsActive == isActive);
        //    }

        //    if (categoryId.HasValue)
        //    {
        //        query = query.Where(sc => sc.ParentCategoryId == categoryId);
        //    }

        //    // 👇 Sort conditionally
        //    if (!isFilterApplied)
        //    {
        //        query = query.OrderByDescending(p => p.SubCategoryId);
        //    }

        //    var subCategories = query.Select(sc => new SubCategoryListViewModel
        //    {
        //        SubCategoryId = sc.SubCategoryId,
        //        ParentCategoryId = sc.ParentCategoryId,
        //        ParentCategoryName = sc.ParentCategory != null ? sc.ParentCategory.CName : "N/A",
        //        SCName = sc.SCName,
        //        SCNameArb = sc.SCNameArb,
        //        SCDescription = sc.SCDescription,
        //        SCDescriptionArb = sc.SCDescriptionArb,
        //        SubCategoryImageBase64 = sc.SubCategoryImage != null
        //            ? "data:image/jpeg;base64," + Convert.ToBase64String(sc.SubCategoryImage)
        //            : null,
        //        IsActive = sc.IsActive,
        //        CreatedAt = sc.CreatedAt
        //    }).ToList();

        //    // 🔁 Preserve filter values for view
        //    ViewBag.FilterName = name;
        //    ViewBag.FilterStatus = status;
        //    ViewBag.FilterCategoryId = categoryId;

        //    // Dropdown
        //    ViewBag.Categories = new SelectList(
        //        _context.Categories.Where(c => c.IsActive),
        //        "CategoryId",
        //        "CName"
        //    );

        //    return View(subCategories);

        //}
        [HttpPost]
        public IActionResult DeleteSubCategory(int id)
        {
            try
            {
                var subCat = _context.SubCategories.Find(id);
                if (subCat == null)
                    return Json(new { success = false, message = "Sub-Category not found!" });

                _context.SubCategories.Remove(subCat);
                var rowsAffected = _context.SaveChanges();

                if (rowsAffected > 0)
                    return Json(new { success = true, message = "Sub-Category deleted successfully!" });
                else
                    return Json(new { success = false, message = "Sub-Category could not be deleted. It may have related data." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting subcategory: {ex.Message}");
                return Json(new { success = false, message = "Sub-Category cannot be deleted due to database constraints." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubCategoryAsync(SubCategory model, IFormFile subCategoryImage)
        {

            //if (subCategoryImage != null && subCategoryImage.Length > 0)
            //{
            //    using (var originalStream = subCategoryImage.OpenReadStream())
            //    using (var originalImage = Image.FromStream(originalStream))
            //    {
            //        model.SubCategoryImage = ImageHelper.CompressImage(originalImage, 800, 800, 70L);
            //    }
            //}
           
            if (subCategoryImage != null && subCategoryImage.Length > 0)
            {
                using var stream = subCategoryImage.OpenReadStream();
                using var image = Image.FromStream(stream);

                bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                byte[] finalImage;

                if (hasAlpha && Path.GetExtension(subCategoryImage.FileName).ToLower() == ".png")
                {
                    finalImage = ImageHelper.CompressPng(image, 800, 800);
                }
                else
                {
                    finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                }

                model.SubCategoryImage = finalImage;
            }
            model.CreatedAt = DateTime.Now;
            model.CreatedBy = 1;

            _context.SubCategories.Add(model);
            await _context.SaveChangesAsync(); // ✅ use async

            TempData["Success"] = "Sub-Category created successfully!";
            return RedirectToAction("List");

            // If model invalid (will never reach due to redirect)
            ViewBag.Categories = new SelectList(
                _context.Categories.Where(c => c.IsActive),
                "CategoryId",
                "CName"
            );
            return View(model);
        }

        // ✅ GET: Edit SubCategory
        public IActionResult Edit(int id)
        {
            var subCategory = _context.SubCategories.Find(id);
            if (subCategory == null)
                return NotFound();

            // Load Parent Categories for dropdown
            ViewBag.Categories = new SelectList(_context.Categories.Where(c => c.IsActive == true), "CategoryId", "CName", subCategory.ParentCategoryId);

            return View(subCategory);
        }

        // ✅ POST: Edit SubCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(SubCategory model, IFormFile subCategoryImage)
        {
            var subCategory = _context.SubCategories.Find(model.SubCategoryId);
            if (subCategory == null)
                return NotFound();

            // ✅ Update basic fields
            subCategory.ParentCategoryId = model.ParentCategoryId;
            subCategory.SCName = model.SCName;
            subCategory.SCNameArb = model.SCNameArb;
            subCategory.SCDescription = model.SCDescription;
            subCategory.SCDescriptionArb = model.SCDescriptionArb;
            subCategory.IsActive = model.IsActive;
            subCategory.UpdatedAt = DateTime.Now;
            subCategory.showinnav = model.showinnav;

            // ✅ Use your helper to compress the image if uploaded
            //if (subCategoryImage != null && subCategoryImage.Length > 0)
            //{
            //    using (var originalStream = subCategoryImage.OpenReadStream())
            //    using (var originalImage = Image.FromStream(originalStream))
            //    {
            //        subCategory.SubCategoryImage = ImageHelper.CompressImage(originalImage, 800, 800, 70L);
            //    }
            //}
            if (subCategoryImage != null && subCategoryImage.Length > 0)
            {
                using var stream = subCategoryImage.OpenReadStream();
                using var image = Image.FromStream(stream);

                bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                byte[] finalImage;

                if (hasAlpha && Path.GetExtension(subCategoryImage.FileName).ToLower() == ".png")
                {
                    // ✅ Transparent PNG
                    finalImage = ImageHelper.CompressPng(image, 800, 800);
                }
                else
                {
                    // ✅ JPG / non-transparent
                    finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                }

                subCategory.SubCategoryImage = finalImage;
            }
            _context.SubCategories.Update(subCategory);
            _context.SaveChanges();

            TempData["Success"] = "Sub-Category updated successfully!";
            return RedirectToAction("List");

            // This line will not be hit because of redirect above,
            // but in case you ever return the view again (e.g., on validation failure), you can use:
            /*
            ViewBag.Categories = new SelectList(
                _context.Categories.Where(c => c.IsActive == true),
                "CategoryId", "CName",
                model.ParentCategoryId
            );
            return View(model);
            */
        }
        [HttpGet]
        public async Task<JsonResult> AutoCompleteSubCategory(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            IQueryable<SubCategory> queryable = _context.SubCategories;

            // Detect if input is Arabic
            bool isArabic = term.Any(c => c >= 0x0600 && c <= 0x06FF);

            if (isArabic)
            {
                queryable = queryable.Where(sc => sc.SCNameArb.Contains(term));
            }
            else
            {
                queryable = queryable.Where(sc => sc.SCName.Contains(term));
            }

            var suggestions = await queryable
                .Select(sc => isArabic ? sc.SCNameArb : sc.SCName)
                .Take(10)
                .ToListAsync();

            return Json(suggestions);
        }
    }
}
