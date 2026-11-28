using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = AdminStaffAccess.Roles)]
    public class BrandController : Controller
    {
        private readonly EcommerceDbContext _context;

        private readonly AdminStaffAccess _access;

        public BrandController(EcommerceDbContext context, AdminStaffAccess access)
        {
            _context = context;
            _access = access;
        }
        [HttpGet]
        public async Task<IActionResult> Index(string? name, int page = 1, int pageSize = 10, bool partial = false)
        {
            if (!(await _access.RightsAsync(User, EcommerceWebApplication.Helper.AdminStaffAccess.Brands, HttpContext.RequestAborted)).View) return Forbid();
            // Check current culture (Arabic or English)
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabicCulture = culture.TextInfo.IsRightToLeft;

            // Base Query
            IQueryable<BrandModel> brandQuery = _context.Brand;

            // ✅ NAME FILTER ONLY (Arabic + English)
            if (!string.IsNullOrWhiteSpace(name))
            {
                bool isArabicInput = name.Any(c => c >= 0x0600 && c <= 0x06FF);

                if (isArabicInput)
                {
                    brandQuery = brandQuery.Where(b => b.BrandNameAr.Contains(name));
                }
                else
                {
                    brandQuery = brandQuery.Where(b => b.BrandNameAEn.Contains(name));
                }
            }

            pageSize = Math.Clamp(pageSize, 1, 100);
            int totalItems = brandQuery.Count();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize)));
            var pagedData = brandQuery.AsNoTracking().OrderByDescending(b => b.BrandId)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(d => new BrandViewModel
                {
                    BrandId = d.BrandId,
                    BrandNameAr = d.BrandNameAr,
                    BrandNameAEn = d.BrandNameAEn,
                    CreatedAt = d.CreatedAt,
                    DiscountPersent = d.DiscountPersent,
                    BrandlogoBase64 = d.BrandLogo != null
                        ? "data:image/png;base64," + Convert.ToBase64String(d.BrandLogo)
                        : null
                })
                .ToList();
            var pagedModel = new PagedResult<BrandViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            // Keep filter value
            ViewBag.FilterName = name;

            return partial ? PartialView("_RegisterRows", pagedModel) : View(pagedModel);
        }

        [HttpGet]
        public async Task<IActionResult> CreateBrand(int? id)
        {
            var rights = await _access.RightsAsync(User, EcommerceWebApplication.Helper.AdminStaffAccess.Brands, HttpContext.RequestAborted);
            if (id.HasValue ? !rights.Edit : !rights.Create) return Forbid();
            if (id == null)
            {
                return View(new BrandModel());  
            }
            var model = _context.Brand.Find(id);
            if (model == null)
            {
                return NotFound();
            }
            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBrand(BrandModel model, IFormFile Brandlogo)
        {
            var rights = await _access.RightsAsync(User, EcommerceWebApplication.Helper.AdminStaffAccess.Brands, HttpContext.RequestAborted);
            if (model.BrandId > 0 ? !rights.Edit : !rights.Create) return Forbid();
            if (!int.TryParse(User.FindFirst("UserId")?.Value, out var actor)) return Forbid();
            try
            {
                //byte[] compressedBytes = null;

                //// ✅ If new image uploaded → compress and save
                //if (Brandlogo != null && Brandlogo.Length > 0)
                //{
                //    using (var originalStream = Brandlogo.OpenReadStream())
                //    using (var originalImage = Image.FromStream(originalStream))
                //    {
                //        compressedBytes = ImageHelper.CompressImage(originalImage, 800, 800, 70L);
                //    }
                //}
                byte[] compressedBytes = null;

                if (Brandlogo != null && Brandlogo.Length > 0)
                {
                    using var stream = Brandlogo.OpenReadStream();
                    using var image = Image.FromStream(stream);

                    bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                    if (hasAlpha && Path.GetExtension(Brandlogo.FileName).ToLower() == ".png")
                    {
                        // ✅ Transparent PNG
                        compressedBytes = ImageHelper.CompressPng(image, 800, 800);
                    }
                    else
                    {
                        // ✅ JPG / normal image
                        compressedBytes = ImageHelper.CompressImage(image, 800, 800, 70L);
                    }
                }

                if (model.BrandId > 0)
                {
                  
                    var brand = _context.Brand.FirstOrDefault(c => c.BrandId == model.BrandId);
                    if (brand == null)
                    {
                        TempData["Error"] = "Brand not found.";
                        return RedirectToAction("Index");
                    }

                    brand.BrandNameAr = model.BrandNameAr;
                    brand.BrandNameAEn = model.BrandNameAEn;
                    brand.DiscountPersent = model.DiscountPersent;
                    brand.Discriptionar = model.Discriptionar;
                    brand.Discriptionen = model.Discriptionen;
                    brand.Updatedby = actor;
                    brand.UpdatedAt = DateTime.Now;

                    if (compressedBytes != null)
                    {
                        brand.BrandLogo = compressedBytes;
                    }
                }
                else
                {
                   
                    if (compressedBytes != null)
                        model.BrandLogo = compressedBytes;

                    model.Createdby = actor;
                    model.CreatedAt = DateTime.Now;

                    _context.Brand.Add(model);
                }

                _context.SaveChanges();
                TempData["Success"] = "Brand saved successfully.";
            }
            catch (Exception ex)
            {
                TempData.Remove("Success");
                TempData["Error"] = "Unable to save brand. Please review the details and try again.";
            }

            return RedirectToAction("Index");
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!(await _access.RightsAsync(User, EcommerceWebApplication.Helper.AdminStaffAccess.Brands, HttpContext.RequestAborted)).Delete) return Forbid();
            try
            {
                var WekendDeals1 = _context.Brand.FirstOrDefault(c => c.BrandId == id);
                if (WekendDeals1 == null)
                {
                    return Json(new { success = false, message = "Brand not found." });
                }

                _context.Brand.Remove(WekendDeals1);
                _context.SaveChanges();

                return Json(new { success = true, message = "Brand deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> AutoCompleteSubCategory(string term)
        {
            if (!(await _access.RightsAsync(User, EcommerceWebApplication.Helper.AdminStaffAccess.Brands, HttpContext.RequestAborted)).View) return Forbid();
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            IQueryable<BrandModel> queryable = _context.Brand;

            // Detect if input is Arabic
            bool isArabic = term.Any(c => c >= 0x0600 && c <= 0x06FF);

            if (isArabic)
            {
                queryable = queryable.Where(sc => sc.BrandNameAr.Contains(term));
            }
            else
            {
                queryable = queryable.Where(sc => sc.BrandNameAEn.Contains(term));
            }

            var suggestions = await queryable
                .Select(sc => isArabic ? sc.BrandNameAr : sc.BrandNameAEn)
                .Take(10)
                .ToListAsync();

            return Json(suggestions);
        }
    }


}


       

