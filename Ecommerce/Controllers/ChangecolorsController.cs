using DocumentFormat.OpenXml.Spreadsheet;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text.RegularExpressions;

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
            return View();
        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public IActionResult SaveUpdate(ColorsModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Colorcode) || !Regex.IsMatch(model.Colorcode, @"\A#[0-9a-fA-F]{6}\z"))
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
                return Json(new { success = false, message = "Unable to save color." });
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
