using DocumentFormat.OpenXml.Spreadsheet;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceWebApplication.Controllers
{
    public class ChangecolorsController : Controller
    {
        private readonly EcommerceDbContext _context;

        public ChangecolorsController(EcommerceDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public IActionResult SaveUpdate(ColorsModel model)
        {
            if (model == null)
                return Json(new { success = false, message = "Invalid data" });

            try
            {
                var existing = _context.colors.FirstOrDefault();

                if (existing == null)
                {
                    // 🔸 Insert (first time)
                    ColorsModel obj = new ColorsModel();
                    obj.Colorcode = model.Colorcode;

                    _context.colors.Add(obj);
                    _context.SaveChanges();

                    return Json(new { success = true, message = "Color saved successfully" });
                }
                else
                {
                 
                    existing.Colorcode = model.Colorcode;

                    _context.SaveChanges();

                    return Json(new { success = true, message = "Color updated successfully" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpGet]
        public IActionResult GetColor()
        {
            var color = _context.colors.FirstOrDefault();

            if (color != null)
            {
                return Json(new { success = true, color = color.Colorcode });
            }
            else
            {
                return Json(new { success = false });
            }
        }
      
    }

}
