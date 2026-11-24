using System;
using System.Text;
using System.Threading.Tasks;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helpers;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Service;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace EcommerceWebApplication.Controllers
{
    public class OtpController : Controller
    {
        //private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private static string _lastOtp;
        private static DateTime _otpExpiry;
        private readonly EcommerceDbContext _context;
        public OtpController(ISmsService smsService, EcommerceDbContext context)
        {
            _smsService = smsService;
            _context = context;
            //_emailService = emailService;
            //_emailService = emailService;
        }

        
        [HttpPost]
        public async Task<IActionResult> Send(string mobile)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (string.IsNullOrEmpty(userId.ToString()))
            {
                return RedirectToAction("MarketLogin", "Market");
            }
            var User = _context.Users.FirstOrDefault(u => u.UserId == userId);
            mobile = User.Phone.Trim();
            var otp = new Random().Next(100000, 999999).ToString();
            _lastOtp = otp;
            _otpExpiry = DateTime.Now.AddMinutes(5);

            using (var httpClient = new HttpClient())
            {
                var url = $"https://raqmikart.com/api/sms/send?mobile={mobile}&message=Dear customer, your OTP from Raqmikart is {otp}";

                // Empty content, because API sirf querystring se data lega
                var response = await httpClient.PostAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "OTP sent successfully" });
                }
                else
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = "Failed to send OTP", error = errorMsg });
                }
            }
        }

        public class OtpVerifyRequest
        {
            public string Otp { get; set; }
        }

        [HttpPost]
        public IActionResult Verify([FromBody] OtpVerifyRequest request)
        {
            string otp = request.Otp;

            if (otp == _lastOtp && DateTime.Now <= _otpExpiry)
            {

                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Invalid or expired OTP." });
        }

        [HttpGet]
        public async Task<IActionResult> Resend()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (string.IsNullOrEmpty(userId.ToString()))
            {
                return RedirectToAction("MarketLogin", "Market");
            }
            var User = _context.Users.FirstOrDefault(u => u.UserId == userId);
           var  mobile = User.Phone.Trim();
            if (string.IsNullOrEmpty(_lastOtp))
                return Json(new { success = false, message = "No OTP session found." });

            // Re-send old OTP or generate new one
            var otp = new Random().Next(100000, 999999).ToString();
            _lastOtp = otp;
            _otpExpiry = DateTime.Now.AddMinutes(5);



            using (var httpClient = new HttpClient())
            {
                var url = $"https://finegoldkw.com/api/sms/send?mobile={mobile}&message=Dear customer, your OTP from Raqmikart is {otp}";

                // Empty content, because API sirf querystring se data lega
                var response = await httpClient.PostAsync(url, null);

                // Send email
                string subject = "Your One-Time Password (OTP)";
                string body = $@"
     <p>Dear User,</p>
     <p>Your One-Time Password (OTP) for accessing your <strong>Raqmikart</strong> account is: <strong>{otp}</strong></p>
     <p>Please use this OTP within the next <strong>5 minutes</strong> to complete your login or verification.</p>
     <p style='color:red;'>For your security, do not share this OTP with anyone.</p>
     <br />
     <p>Thank you,<br />Team Raqmikart Webisuite</p>";

              //  _emailService.Send(User.Email, subject, body);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "OTP sent successfully" });
                }
                else
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = "Failed to send OTP", error = errorMsg });
                }
            }

        }
    }
}
