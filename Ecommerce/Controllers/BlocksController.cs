using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    [Authorize]
    public class BlocksController : Controller
    {
        private readonly EcommerceDbContext _context;

        public BlocksController(EcommerceDbContext context)
        {
            _context = context;
        }

        // ✅ List blocks with Area name using join
        public IActionResult BlockList(string? name, int page = 1, int pageSize = 10)
        {
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

            var blocks = query.OrderByDescending(b => b.Id).ToList();
            int totalItems = blocks.Count;
            var pagedData = blocks
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
        public IActionResult Create()
        {
            ViewBag.Areas = new SelectList(_context.Area.ToList(), "Id", "AreaName");
            return View();
        }

        // ✅ POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Block model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Areas = new SelectList(_context.Area.ToList(), "Id", "AreaName", model.AreaId);
                return View(model);
            }

            _context.Block.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Block created successfully!";
            return RedirectToAction("BlockList");
        }

        // ✅ GET: Edit
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
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
            if (!ModelState.IsValid)
            {
                ViewBag.Areas = new SelectList(_context.Area.ToList(), "Id", "AreaName", model.AreaId);
                return View(model);
            }

            var block = await _context.Block.FindAsync(model.Id);
            if (block == null) return NotFound();

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
        public async Task<JsonResult> AutoCompleteArea(string term)
        {
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