using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = AdminStaffAccess.Roles)]
    public class AreasController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly AdminStaffAccess _access;

        public AreasController(EcommerceDbContext context, AdminStaffAccess access)
        {
            _context = context;
            _access = access;
        }

        [HttpGet]
        public async Task<IActionResult> AreaList(string? name, int page = 1, int pageSize = 10)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Areas, HttpContext.RequestAborted)).View) return Forbid();
            var areasQuery = _context.Area.AsQueryable();

            // Apply filter
            if (!string.IsNullOrWhiteSpace(name))
            {
                areasQuery = areasQuery.Where(a =>
                    a.AreaName.Contains(name) || a.AreaNameArb.Contains(name));
            }

            // Sort by latest
            areasQuery = areasQuery.OrderByDescending(a => a.Id);

            ViewBag.FilterName = name;
            pageSize = Math.Clamp(pageSize,1,100);
            int totalItems = await areasQuery.CountAsync();
            page = Math.Clamp(page,1,Math.Max(1,(int)Math.Ceiling((double)totalItems/pageSize)));
            var pagedData = areasQuery.AsNoTracking()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedModel = new PagedResult<Area>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
            return View(pagedModel);
        }

        // ✅ GET: Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Areas, HttpContext.RequestAborted)).Create) return Forbid();
            return View();
        }

        // ✅ POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Area model)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Areas, HttpContext.RequestAborted)).Create) return Forbid();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Id != 0) return BadRequest();
            _context.Area.Add(new Area { AreaName=model.AreaName, AreaNameArb=model.AreaNameArb });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Area created successfully!";
            return RedirectToAction("AreaList");
        }

        // ✅ GET: Edit
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Areas, HttpContext.RequestAborted)).Edit) return Forbid();
            var area = await _context.Area.FindAsync(id);

            if (area == null)
                return NotFound();

            return View(area);
        }

        // ✅ POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Area model)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Areas, HttpContext.RequestAborted)).Edit) return Forbid();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var area = await _context.Area.FindAsync(model.Id);
            if (area == null)
                return NotFound();

            area.AreaName = model.AreaName;
            area.AreaNameArb = model.AreaNameArb;

            _context.Area.Update(area);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Area updated successfully!";
            return RedirectToAction("AreaList");
        }

        // ✅ AJAX Delete
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteArea(int id)
        {
            var area = await _context.Area.FindAsync(id);

            if (area == null)
                return Json(new { success = false, message = "Area not found!" });

            _context.Area.Remove(area);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Area deleted successfully!" });
        }

        // ✅ For AutoComplete (Optional)
        [HttpGet]
        public async Task<IActionResult> AutoCompleteArea(string term)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Areas, HttpContext.RequestAborted)).View) return Forbid();
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            bool isArabic = term.Any(c => c >= 0x0600 && c <= 0x06FF);

            var query = _context.Area.AsQueryable();

            if (isArabic)
            {
                query = query.Where(a => a.AreaNameArb.Contains(term));
            }
            else
            {
                query = query.Where(a => a.AreaName.Contains(term));
            }

            var suggestions = await query
                .Select(a => isArabic ? a.AreaNameArb : a.AreaName)
                .Take(10)
                .ToListAsync();

            return Json(suggestions);
        }
    }
}
