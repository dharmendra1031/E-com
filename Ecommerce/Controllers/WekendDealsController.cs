using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
    public class WekendDealsController : Controller
    {
        private readonly EcommerceDbContext _context;

        public WekendDealsController(EcommerceDbContext context)
        {
            _context = context;
        }


        [HttpGet]
        public IActionResult WekendDealsList(string? name, int page = 1, int pageSize = 10)
        {
            // Check current culture (Arabic or English)
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabicCulture = culture.TextInfo.IsRightToLeft;

            // Base Query
            IQueryable<WekendDealsModel> dealsQuery = _context.WekendDeals;

            // ✅ NAME FILTER ONLY (Arabic + English)
            if (!string.IsNullOrWhiteSpace(name))
            {
                bool isArabicInput = name.Any(c => c >= 0x0600 && c <= 0x06FF);

                if (isArabicInput)
                {
                    dealsQuery = dealsQuery.Where(d => d.WdNameAr.Contains(name));
                }
                else
                {
                    dealsQuery = dealsQuery.Where(d => d.WdNameEn.Contains(name));
                }
            }

            // Convert to ViewModel
            var deals = dealsQuery
                .Select(d => new WekendDealsViewModel
                {
                    WdId = d.WdId,
                    WdNameAr = d.WdNameAr,
                    WdNameEn = d.WdNameEn,
                    DiscPer = d.DiscPer,
                    FromDate = d.FromDate,
                    ToDate = d.ToDate,
                    MaxQuantity = d.MaxQuantity
                })
                .ToList();

            // Pagination
            int totalItems = deals.Count;

            var pagedData = deals
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedModel = new PagedResult<WekendDealsViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            // 🔹 Keep filter value
            ViewBag.FilterName = name;

            return View(pagedModel);
        }

        [HttpGet]
        public JsonResult GetProductPrice(int id)
        {
            var price = _context.Products
                .Where(x => x.ProductId == id)
                .Select(x => x.Price)
                .FirstOrDefault();

            return Json(price);
        }

        public IActionResult CreateDeals(int? id)
        {
            // Dropdown Products
            ViewBag.Products = _context.Products.Select(p => new SelectListItem
            {
                Value = p.ProductId.ToString(),
                Text = p.Name
            }).ToList();

            // ---------- CREATE ----------
            if (id == null)
            {
                return View(new WekendDealsModel());   // empty model → Create form
            }

            // ---------- EDIT ----------
            var model = _context.WekendDeals.Find(id);

            if (model == null)
            {
                return NotFound();
            }

            // Convert nullable date to string
       
            model.FromDateStr = Convert.ToDateTime(model.FromDate).ToString("dd-MM-yyyy");
            model.ToDateStr = Convert.ToDateTime(model.ToDate).ToString("dd-MM-yyyy");

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateDealssave(WekendDealsModel model)
        {
            try
            {
                if (!string.IsNullOrEmpty(model.FromDateStr))
                    model.FromDate = DateTime.ParseExact(model.FromDateStr, "dd-MM-yyyy", CultureInfo.InvariantCulture);

                if (!string.IsNullOrEmpty(model.ToDateStr))
                    model.ToDate = DateTime.ParseExact(model.ToDateStr, "dd-MM-yyyy", CultureInfo.InvariantCulture);
                bool isDealExists = _context.WekendDeals
           .Any(d =>
               d.ProductId == model.ProductId &&
               d.WdId != model.WdId &&               // ignore current deal if updating
               d.ToDate >= DateTime.Now               // still valid
           );

                if (isDealExists)
                {
                    TempData["Error"] = "A weekend deal for this product is already active!";
                    return RedirectToAction("WekendDealsList");
                }

                if (model.WdId > 0)   // Update
                {
                    var coupon = _context.WekendDeals.FirstOrDefault(c => c.WdId == model.WdId);
                    if (coupon == null)
                    {
                        TempData["Error"] = "Weekend deal not found.";
                        return RedirectToAction("WekendDealsList");
                    }

                    coupon.WdNameAr = model.WdNameAr;
                    coupon.WdNameEn = model.WdNameEn;
                    coupon.ProductId = model.ProductId;
                    coupon.DiscPer = model.DiscPer;
                    coupon.FromDate = model.FromDate;
                    coupon.ToDate = model.ToDate;
                    coupon.MaxQuantity = model.MaxQuantity;
                    coupon.UpdatedBy = 1;
                    coupon.UpdatedAt = DateTime.Now;


                }
                else  // Save
                {
                    model.CreatedBy = 1;
                    model.CreatedAt = DateTime.Now;

                    _context.WekendDeals.Add(model);


                }

                _context.SaveChanges();
                TempData["Success"] = "Weekend deal saved successfully.";
            }
            catch (Exception ex)
            {
                TempData.Remove("Success");
                TempData["Error"] = "Unable to save weekend deal. Review the details before retrying.";
            }

            return RedirectToAction("WekendDealsList");
        }


        [HttpPost]
        public IActionResult Delete(int id)
        {
            try
            {
                var WekendDeals1 = _context.WekendDeals.FirstOrDefault(c => c.WdId == id);
                if (WekendDeals1 == null)
                {
                    return Json(new { success = false, message = "WekendDeals not found." });
                }

                _context.WekendDeals.Remove(WekendDeals1);
                _context.SaveChanges();

                return Json(new { success = true, message = "WekendDeals deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Unable to delete weekend deal." });
            }
        }
        [HttpGet]
        public async Task<JsonResult> AutoCompletedeal(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            IQueryable<WekendDealsModel> queryable = _context.WekendDeals;

            // Detect if input is Arabic
            bool isArabic = term.Any(c => c >= 0x0600 && c <= 0x06FF);

            if (isArabic)
            {
                queryable = queryable.Where(sc => sc.WdNameAr.Contains(term));
            }
            else
            {
                queryable = queryable.Where(sc => sc.WdNameEn.Contains(term));
            }

            var suggestions = await queryable
                .Select(sc => isArabic ? sc.WdNameAr : sc.WdNameEn)
                .Take(10)
                .ToListAsync();

            return Json(suggestions);
        }

    }
}
