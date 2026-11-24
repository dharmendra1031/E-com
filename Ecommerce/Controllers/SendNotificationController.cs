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
    [Authorize]
    public class SendNotificationController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly IEmailService _emailService;

        public SendNotificationController(EcommerceDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }
        public IActionResult Index()
        {
            var model = new SendNotificationModel
            {
                UserEmails = _context.Customers
                    .Where(u => !string.IsNullOrEmpty(u.Email))
                    .Select(u => new Customers
                    {
                        UserId = u.UserId,
                        Email = u.Email
                    })
                    .ToList()
            };

            return View(model);
        }
        [HttpPost]
        public IActionResult SaveNotification(SendNotificationModel model, string UserEmails)
        {

            if (!string.IsNullOrEmpty(UserEmails))
            {
                var emailList = UserEmails
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(email => email.Trim())
                    .ToList();

                try
                {
                    _emailService.SendBulkEmails(emailList, model.EmailSubject, model.Body);

                    // ✅ Save to Database
                    var notification = new SendNotificationModel
                    {
                        Customertype=model.Customertype,
                        EmailSubject = model.EmailSubject,
                        Body = model.Body,
                        CreatedAt = DateTime.Now,
                        CreatedBy = 1
                    };

                    _context.SendNotification.Add(notification);
                    _context.SaveChanges();
                    TempData["Success"] = "Notification sent and saved successfully.";
                    //if (culture == "en")
                    //{
                    //    TempData["Success"] = "Notification sent and saved successfully.";

                    //}
                    //else
                    //{
                    //    TempData["Success"] = "تم إرسال الإشعار وحفظه بنجاح.";
                    //}

                }
                catch (Exception ex)
                {
                    //if (culture == "en")
                    //{
                    //    TempData["Error"] = "Failed to send or save notification: " + ex.Message;

                    //}
                    //else
                    //{
                    //    TempData["Error"] = "فشل في إرسال أو حفظ الإشعار:" + ex.Message;
                    //}
                    TempData["Error"] = "Failed to send or save notification: " + ex.Message;

                }
            }
            else
            {
                //if (culture == "en")
                //{
                //    TempData["Error"] = "Please select users before sending the notification.";

                //}
                //else
                //{
                //    TempData["Error"] = "يرجى تحديد المستخدمين قبل إرسال الإشعار.";
                //}
                TempData["Error"] = "Please select users before sending the notification.";

            }

            return RedirectToAction("Index");
        }

    }
}
