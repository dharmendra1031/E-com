using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SendNotificationController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly IEmailService _emailService;

        public SendNotificationController(EcommerceDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }
        public IActionResult Index() => View(new SendNotificationModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveNotification(SendNotificationModel model)
        {
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
                    CreatedBy = int.TryParse(User.FindFirst("UserId")?.Value, out var id) ? id : 1
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
