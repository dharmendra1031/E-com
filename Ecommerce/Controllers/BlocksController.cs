using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = AdminStaffAccess.Roles)]
    public class BlocksController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly AdminStaffAccess _access;

        public BlocksController(EcommerceDbContext context, AdminStaffAccess access)
        {
            _context = context;
            _access = access;
        }

        // ✅ List blocks with Area name using join
        [HttpGet]
        public async Task<IActionResult> BlockList(string? name, int page = 1, int pageSize = 10)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Blocks, HttpContext.RequestAborted)).View) return Forbid();
            var query = from b in _context.Block
                        join a in _context.Area on b.AreaId equals a.Id
                        select new BlockViewModel
                        {
                            Id = b.Id,
                            BlockName = b.BlockName,
                            BlockNameArb = b.BlockNameArb,
                            AreaId = b.AreaId,
                            AreaName = a.AreaName
                        };

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(b => b.BlockName.Contains(name) || b.BlockNameArb.Contains(name) || b.AreaName.Contains(name));
            }

            pageSize = Math.Clamp(pageSize,1,100);
            int totalItems = await query.CountAsync();
            page = Math.Clamp(page,1,Math.Max(1,(int)Math.Ceiling((double)totalItems/pageSize)));
            var pagedData = query.OrderByDescending(b => b.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedModel = new PagedResult<BlockViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
            ViewBag.FilterName = name;
            return View(pagedModel);
        }

        // ✅ GET: Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Blocks, HttpContext.RequestAborted)).Create) return Forbid();
            ViewBag.Areas = new SelectList(_context.Area.ToList(), "Id", "AreaName");
            return View();
        }

        // ✅ POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Block model)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Blocks, HttpContext.RequestAborted)).Create) return Forbid();
            if (!ModelState.IsValid)
            {
                ViewBag.Areas = new SelectList(_context.Area.ToList(), "Id", "AreaName", model.AreaId);
                return View(model);
            }

            if (model.Id != 0 || !await _context.Area.AnyAsync(a=>a.Id==model.AreaId)) return BadRequest();
            _context.Block.Add(new Block { BlockName=model.BlockName, BlockNameArb=model.BlockNameArb, AreaId=model.AreaId });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Block created successfully!";
            return RedirectToAction("BlockList");
        }

        // ✅ GET: Edit
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Blocks, HttpContext.RequestAborted)).Edit) return Forbid();
            var block = await _context.Block.FindAsync(id);
            if (block == null) return NotFound();

            ViewBag.Areas = new SelectList(_context.Area.ToList(), "Id", "AreaName", block.AreaId);
            return View(block);
        }

        // ✅ POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Block model)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Blocks, HttpContext.RequestAborted)).Edit) return Forbid();
            if (!ModelState.IsValid)
            {
                ViewBag.Areas = new SelectList(_context.Area.ToList(), "Id", "AreaName", model.AreaId);
                return View(model);
            }

            var block = await _context.Block.FindAsync(model.Id);
            if (block == null) return NotFound();

            if (!await _context.Area.AnyAsync(a=>a.Id==model.AreaId)) return BadRequest();
            // Existing addresses may reference the original area/block pair.
            if (AdminStaffAccess.IsRestricted(User) && block.AreaId != model.AreaId) return Forbid();
            block.BlockName = model.BlockName;
            block.BlockNameArb = model.BlockNameArb;
            block.AreaId = model.AreaId;

            _context.Block.Update(block);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Block updated successfully!";
            return RedirectToAction("BlockList");
        }

        // ✅ AJAX Delete
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBlock(int id)
        {
            var block = await _context.Block.FindAsync(id);
            if (block == null)
                return Json(new { success = false, message = "Block not found!" });

            _context.Block.Remove(block);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Block deleted successfully!" });
        }
        [HttpGet]
        public async Task<IActionResult> AutoCompleteArea(string term)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Blocks, HttpContext.RequestAborted)).View) return Forbid();
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            bool isArabic = term.Any(c => c >= 0x0600 && c <= 0x06FF);

            var query = _context.Block.AsQueryable();

            if (isArabic)
            {
                query = query.Where(a => a.BlockNameArb.Contains(term));
            }
            else
            {
                query = query.Where(a => a.BlockName.Contains(term));
            }

            var suggestions = await query
                .Select(a => isArabic ? a.BlockNameArb : a.BlockName)
                .Take(10)
                .ToListAsync();

            return Json(suggestions);
        }
    }

}