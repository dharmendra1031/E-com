using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EcommerceWebApplication.Helper;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    public class ChangecolorsController : Controller
    {
        private readonly EcommerceDbContext _context;

        public ChangecolorsController(EcommerceDbContext context)
        {
            _context = context;
        }
        [Authorize(Roles = "Admin")]
        public IActionResult Index()
        {
            return View(StoreTheme.From(_context.colors.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Colorcode).FirstOrDefault()));
        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult SaveUpdate(ColorsModel model)
        {
            if (model == null || !StoreTheme.IsValid(model.Colorcode))
                return Json(new { success = false, message = "Invalid data" });

            try
            {
                var existing = _context.colors.OrderBy(c => c.Id).FirstOrDefault();

                if (existing == null)
                {
                    // 🔸 Insert (first time)
                    ColorsModel obj = new ColorsModel();
                    obj.Colorcode = StoreTheme.From(model.Colorcode).Primary;

                    _context.colors.Add(obj);
                    _context.SaveChanges();

                    return Json(new { success = true, message = "Color saved successfully" });
                }
                else
                {
                 
                    existing.Colorcode = StoreTheme.From(model.Colorcode).Primary;

                    _context.SaveChanges();

                    return Json(new { success = true, message = "Color updated successfully" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Unable to save color." });
            }
        }
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult GetColor()
        {
            var color = _context.colors.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Colorcode).FirstOrDefault();
            var theme = StoreTheme.From(color);
            return Json(new { success = StoreTheme.IsValid(color), color = theme.Primary, theme });
        }
    }
}
