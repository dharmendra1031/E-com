using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = AdminStaffAccess.Roles)]
    public class SendNotificationController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly AdminStaffAccess _access;
        private readonly IEmailService _emailService;

        public SendNotificationController(EcommerceDbContext context, IEmailService emailService, AdminStaffAccess access)
        {
            _context = context;
            _access = access;
            _emailService = emailService;
        }
        [HttpGet]
        public async Task<IActionResult> Index(int page=1)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Notifications, HttpContext.RequestAborted)).View) return Forbid();
            page=Math.Clamp(page,1,100000);
            var rows=await _context.SendNotification.AsNoTracking().OrderByDescending(x=>x.Id)
                .Skip((page-1)*25).Take(26).ToListAsync(HttpContext.RequestAborted);
            ViewBag.History=rows.Take(25).ToList();
            ViewBag.Page=page;
            ViewBag.HasNext=rows.Count>25;
            return View(new SendNotificationModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult SaveNotification(SendNotificationModel model)
        {
            if (!int.TryParse(User.FindFirst("UserId")?.Value,out var actor) || actor<=0) return Forbid();
            if (model.Customertype != "AllUsers" || string.IsNullOrWhiteSpace(model.EmailSubject) ||
                string.IsNullOrWhiteSpace(model.Body))
            {
                TempData["Error"] = "Select recipients and enter the subject and message.";
                return RedirectToAction("Index");
            }

            // Resolve recipients on the server; never accept arbitrary email addresses from a form.
            var emailList = _context.Customers
                .Where(u => !string.IsNullOrEmpty(u.Email))
                .Select(u => u.Email.Trim())
                .Distinct()
                .ToList();

            if (emailList.Count == 0)
            {
                TempData["Error"] = "No recipients found.";
                return RedirectToAction("Index");
            }

            try
            {
                _emailService.SendBulkEmails(emailList, model.EmailSubject, model.Body);
                var notification = new SendNotificationModel
                {
                    Customertype = model.Customertype,
                    EmailSubject = model.EmailSubject,
                    Body = model.Body,
                    CreatedAt = DateTime.Now,
                    CreatedBy = actor
                };
                _context.SendNotification.Add(notification);
                _context.SaveChanges();
                TempData["Success"] = "Notification sent and saved successfully.";
            }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<SendNotificationController>>()
                    .LogError(ex, "Notification send or save failed.");
                TempData["Error"] = "Delivery may be incomplete. Check the result before sending again.";
            }

            return RedirectToAction("Index");
        }
    }
}
