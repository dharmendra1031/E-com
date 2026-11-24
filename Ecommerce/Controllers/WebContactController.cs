using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceWebApplication.Controllers
{
    public class WebContactController : Controller
    {
        private readonly EcommerceDbContext _context;

        public WebContactController(EcommerceDbContext context)
        {
            _context = context;
        }
        public IActionResult Contact()
        {

            var companyData = _context.CompanyDetails.FirstOrDefault();

            if (companyData == null)
            {
                ViewBag.Email = "";
                ViewBag.Phone = "";
                ViewBag.Address = "";
                return View();
            }

            // ✅ ViewBag me direct values
            ViewBag.Email = companyData.Email;
            ViewBag.Phone = companyData.Phone;
            ViewBag.Address = companyData.Address;

            return View();
        }
        [HttpPost]
        public async Task<IActionResult> SubmitMessage([FromBody] ContactMessage message)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;

            // ✅ validation block (FIXED)
            if (string.IsNullOrWhiteSpace(message.FullName) ||
                string.IsNullOrWhiteSpace(message.Email) ||
                string.IsNullOrWhiteSpace(message.Message))
            {
                if (isArabic)
                {
                    return BadRequest(new { success = false, message = "جميع الحقول مطلوبة" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "All fields are required" });
                }
            }

            // ✅ data save
            message.SubmittedAt = DateTime.Now;
            _context.ContactMessages.Add(message);
            await _context.SaveChangesAsync();

            // ✅ success response
            if (isArabic)
            {
                return Ok(new { success = true, message = "تم إرسال الرسالة بنجاح." });
            }
            else
            {
                return Ok(new { success = true, message = "Message submitted successfully." });
            }
        }



    }
}
