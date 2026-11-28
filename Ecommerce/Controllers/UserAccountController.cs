using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    public class UserAccountController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly IEmailService _emailService;
        public UserAccountController(EcommerceDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // ===================== REGISTER =====================
        [HttpGet]
        public IActionResult Register(string? returnUrl)
        {
            var companies = _context.CompanyDetails
                    .Select(c => new CompanyDetails
                    {
                        FacebookUrl = c.FacebookUrl,
                        TwitterUrl = c.TwitterUrl,
                        InstagramUrl = c.InstagramUrl,
                        LinkedInUrl = c.LinkedInUrl,


                    })
                    .ToList();

            var model = new RegisterViewModals
            {
                CompanyDetails = companies,
                ReturnUrl = returnUrl
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModals model)
        {
            if (!CustomerPassword.IsValidNew(model.Password))
                ModelState.AddModelError(nameof(model.Password), "Use 8–50 characters.");
            if (model.BirthDate == default || model.BirthDate.Date > DateTime.Today)
                ModelState.AddModelError(nameof(model.BirthDate), "Enter a valid birth date.");
            if (!string.Equals(HttpContext.Session.GetString("RegistrationVerifiedEmail"), model.Email, StringComparison.OrdinalIgnoreCase) ||
                !long.TryParse(HttpContext.Session.GetString("RegistrationVerifiedUntil"), out var until) ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() > until)
                ModelState.AddModelError(nameof(model.Email), "Verify your email first.");
            if (!ModelState.IsValid) return View(model);

            // Check if email already exists
            bool emailExists = await _context.Customers
                .AnyAsync(c => c.Email == model.Email && c.IsDeleted == 0);

            if (emailExists)
            {
                ModelState.AddModelError("Email", "Email already registered");
                return View(model);
            }

            // Create new customer
            var customer = new Customers
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone =  model.Phone,
                PasswordHash = CustomerPassword.Hash(model.Password),
                IsActive = true,
                RoleId = 2,
                CreatedAt = DateTime.UtcNow,
                BirthDate = model.BirthDate,
                CustType = "Customer" ,
                IsDeleted=0
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove("RegistrationVerifiedEmail");
            HttpContext.Session.Remove("RegistrationVerifiedUntil");

            string deviceId = DeviceIdHelper.GetOrCreateDeviceId(HttpContext);

            if (!string.IsNullOrEmpty(deviceId))
            {
                // 🔥 only guest cart items (UserId == 0)
                var deviceCartItems = await _context.CartItems
                    .Where(c => c.DeviceId == deviceId && c.UserId == 0)
                    .ToListAsync();

                foreach (var item in deviceCartItems)
                {
                    // 🔍 check same product already exists for logged-in user
                    var userCartItem = await _context.CartItems.FirstOrDefaultAsync(c =>
                        c.UserId == customer.UserId &&
                        c.ProductId == item.ProductId
                    );

                    if (userCartItem != null)
                    {
                        // ➕ merge quantity
                        userCartItem.Quantity += item.Quantity;

                        // ❌ remove guest item
                        _context.CartItems.Remove(item);
                    }
                    else
                    {
                        // 🔁 just update UserId (NO NEW INSERT)
                        item.UserId = customer.UserId;
                    }
                }

                await _context.SaveChangesAsync();
            }
          
            // ================= SESSION =================
            HttpContext.Session.SetInt32("UserId", customer.UserId);
            HttpContext.Session.SetString("FullName", customer.FullName);
            HttpContext.Session.SetString("Email", customer.Email);
            HttpContext.Session.SetString("Role", "Customer");

            // ================= COOKIE LOGIN =================
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, customer.FullName),
        new Claim(ClaimTypes.Email, customer.Email),
        new Claim(ClaimTypes.Role, "Customer"),
        new Claim("UserId", customer.UserId.ToString())
    };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                //new AuthenticationProperties
                //{
                //    IsPersistent = true,
                //    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                //}
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    AllowRefresh = true
                }
            );

            // ================= DONE =================

            // 🔹 DEVICE ID
            if (Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);   // 🔥 Checkout pe wapas
            }
            return RedirectToAction("Index", "WebHome");
        }

      
        [HttpPost]
        public async Task<IActionResult> SendOtp([FromBody] OtpRequest request)
        {
            try
            {
                var currentCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;


                string emailExistsMsg = currentCulture == "ar"
                    ? "البريد الإلكتروني موجود مسبقًا."
                    : "Email already exists.";

                string userNotFoundMsg = currentCulture == "ar"
            ? "المستخدم غير موجود."
            : "User not found.";

                string otpSentMsg = currentCulture == "ar"
                    ? "تم إرسال رمز التحقق (OTP) بنجاح."
                    : "OTP sent successfully.";

                string failedToSendMsg = currentCulture == "ar"
                    ? "فشل إرسال كلمة المرور لمرة واحدة ."
                    : "Failed to send OTP.";

                string emailSubject = currentCulture == "ar"
                    ? "كلمة مرورك لمرة واحدة "
                    : "Your One-Time Password (OTP)";

                if (request == null || (string.IsNullOrEmpty(request.email) && string.IsNullOrEmpty(request.mobile)))
                {
                    return BadRequest(new { success = false, message = "Email or Mobile required" });
                }

                Customers user = null;

                if (!string.IsNullOrEmpty(request.email))
                {
                    user = _context.Customers
                        .FirstOrDefault(x => x.Email == request.email && x.IsDeleted == 0);
                }
                else if (!string.IsNullOrEmpty(request.mobile))
                {
                    user = _context.Customers
                        .FirstOrDefault(x => x.Phone == request.mobile && x.IsDeleted == 0);
                }

                // Forgot Password → must exist
                if (request.fp == "fp" && user == null)
                {
                    return BadRequest(new { success = false, message = userNotFoundMsg });
                }

                // Registration → must NOT exist
                if (request.fp != "fp" && user != null)
                {
                    return BadRequest(new { success = false, message = emailExistsMsg });
                }
                var otp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

                string emailBody = currentCulture == "ar"
                    ? $@"
                <p>عزيزي المستخدم،</p>
                <p>رمز التحقق الخاص بك للدخول إلى حساب <strong>تعاونية صباحية</strong> هو: 
                <strong>{otp}</strong></p>
                <p>يرجى استخدام هذا الرمز خلال <strong>5 دقائق</strong> لإكمال عملية الدخول أو التحقق.</p>
                <p style='color:red;'>لأمانك، لا تشارك هذا الرمز مع أي شخص.</p>
                <br />
                <p>شكرًا لك،<br />فريق موقع صباحية</p>"
                    : $@"
                <p>Dear User,</p>
                <p>Your One-Time Password (OTP) for accessing your <strong>Raqmikart</strong> account is: 
                <strong>{otp}</strong></p>
                <p>Please use this OTP within the next <strong>5 minutes</strong> to complete your login or verification.</p>
                <p style='color:red;'>For your security, do not share this OTP with anyone.</p>
                <br />
                <p>Thank you,<br />Team Raqmikart Website</p>";

                //if (_context.Customers.Any(u => u.Email == request.email && u.CustType == "Customer" && u.IsDeleted == 0))
                //    return BadRequest(new { message = emailExistsMsg });
                if (_context.Customers.Any(u => u.Email == request.email && u.CustType == "Customer" && u.IsDeleted == 0) && request.fp == string.Empty)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = emailExistsMsg
                    });
                }

                emailBody = emailBody.Replace(new string('0', 6), otp);
                //_emailService.Send("rickygupta0509@gmail.com", "Test", "Test email");
                if (!string.IsNullOrEmpty(request.email))
                {
                    _emailService.Send(request.email, emailSubject, emailBody);
                }
                //send SMS
                if (!string.IsNullOrEmpty(request.mobile))
                {
                    using (var httpClient = new HttpClient())
                    {
                        var message = Uri.EscapeDataString($"Dear customer, your OTP from Raqmikart is {otp}");
                        var url = $"https://raqmikart.com/api/sms/send?mobile={request.mobile}&message={message}";

                        var response = await httpClient.PostAsync(url, null);

                        if (!response.IsSuccessStatusCode)
                        {
                            var error = await response.Content.ReadAsStringAsync();

                            return Json(new
                            {
                                success = false,
                                message = "SMS sending failed",
                                error = error
                            });
                        }
                    }
                }

                var key = request.email ?? request.mobile;
                HttpContext.Session.Remove("PasswordResetVerifiedEmail");
                HttpContext.Session.Remove("PasswordResetVerifiedUntil");
                HttpContext.Session.Remove("PasswordResetPendingEmail");
                HttpContext.Session.Remove("RegistrationPendingEmail");
                HttpContext.Session.Remove("RegistrationVerifiedEmail");
                HttpContext.Session.Remove("RegistrationVerifiedUntil");
                if (!string.IsNullOrWhiteSpace(request.email))
                    HttpContext.Session.SetString(request.fp == "fp" ? "PasswordResetPendingEmail" : "RegistrationPendingEmail", request.email);

                var existing = _context.UserOtps.FirstOrDefault(x => x.Email == key);

                if (existing != null)
                {
                    existing.Otp = otp;
                    existing.Expiry = DateTime.Now.AddMinutes(5);
                    existing.IsVerified = false;
                }
                else
                {
                    _context.UserOtps.Add(new UserOtp
                    {
                        Email = key, // store email OR mobile
                        Otp = otp,
                        Expiry = DateTime.Now.AddMinutes(5),
                        IsVerified = false
                    });
                }

                await _context.SaveChangesAsync();


                return Ok(new
                {
                    success = true,
                    message = otpSentMsg
                });
            }
            catch (Exception ex)
            {
                var currentCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
                string failedToSendMsg = currentCulture == "ar"
                    ? "فشل إرسال كلمة المرور لمرة واحدة ."
                    : "Failed to send OTP.";

                return StatusCode(500, new { message = failedToSendMsg, error = ex.Message });
            }
        }
        public class OtpVerifyRequest
        {
            public string email { get; set; }
            public string otp { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOtpRegister([FromBody] OtpVerifyRequest req)
        {
            try
            {
                var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

                string invalidOtpMsg = culture == "ar"
                    ? "كلمة مرور لمرة واحدة غير صالحة أو منتهية الصلاحية."
                    : "Invalid or expired OTP";

                string successMsg = culture == "ar"
                    ? "تم التحقق من رمز التفعيل بنجاح."
                    : "OTP verified successfully";

                if (req == null || string.IsNullOrWhiteSpace(req.email) ||
                    !(string.Equals(HttpContext.Session.GetString("PasswordResetPendingEmail"), req.email, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(HttpContext.Session.GetString("RegistrationPendingEmail"), req.email, StringComparison.OrdinalIgnoreCase)))
                    return Ok(new { success = false, message = invalidOtpMsg });

                var otpRecord = await _context.UserOtps
                    .FirstOrDefaultAsync(x => x.Email == req.email);

                if (otpRecord == null ||
                    otpRecord.Otp != req.otp ||
                    otpRecord.Expiry < DateTime.Now ||
                    otpRecord.IsVerified)
                {
                    return Ok(new { success = false, message = invalidOtpMsg });
                }

                otpRecord.IsVerified = true;
                otpRecord.Expiry = DateTime.Now;
                await _context.SaveChangesAsync();

                if (string.Equals(HttpContext.Session.GetString("PasswordResetPendingEmail"), req.email, StringComparison.OrdinalIgnoreCase))
                {
                    HttpContext.Session.Remove("PasswordResetPendingEmail");
                    HttpContext.Session.SetString("PasswordResetVerifiedEmail", req.email);
                    HttpContext.Session.SetString("PasswordResetVerifiedUntil", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString());
                }

                else if (string.Equals(HttpContext.Session.GetString("RegistrationPendingEmail"), req.email, StringComparison.OrdinalIgnoreCase))
                {
                    HttpContext.Session.Remove("RegistrationPendingEmail");
                    HttpContext.Session.SetString("RegistrationVerifiedEmail", req.email);
                    HttpContext.Session.SetString("RegistrationVerifiedUntil", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString());
                }
                return Ok(new { success = true, message = successMsg });
            }
            catch
            {
                return Ok(new { success = false, message = "Verification failed" });
            }
        }

        // ===================== LOGIN =====================
        [HttpGet]
        public IActionResult Login(string? returnUrl)
        {
            var companies = _context.CompanyDetails
                   .Select(c => new CompanyDetails
                   {
                       FacebookUrl = c.FacebookUrl,
                       TwitterUrl = c.TwitterUrl,
                       InstagramUrl = c.InstagramUrl,
                       LinkedInUrl = c.LinkedInUrl,


                   })
                               .ToList();

            var model = new LoginViewModals
            {
                CompanyDetails = companies,
                ReturnUrl = returnUrl
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModals model, string? returnUrl)
        {
            if (!ModelState.IsValid)
                return View(model);

            var customer = await _context.Customers
        .Include(c => c.Role )
        .FirstOrDefaultAsync(c => c.Email == model.Email && c.IsActive && c.IsDeleted == 0 && c.CustType == "Customer" && c.Role.IsActive && c.Role.RoleName == "Customer");

            if (customer == null || !CustomerPassword.Verify(customer.PasswordHash, model.Password, out var upgrade))
            {
                ModelState.AddModelError("", "Invalid email or password");
                return View(model);
            }

            if (upgrade)
            {
                customer.PasswordHash = CustomerPassword.Hash(model.Password);
                await _context.SaveChangesAsync();
            }

            // ----------------- SESSION -----------------
            if (customer.CustType == "Customer")
            {
                HttpContext.Session.SetInt32("UserId", customer.UserId);
                HttpContext.Session.SetString("FullName", customer.FullName);
                HttpContext.Session.SetString("Email", customer.Email);
                HttpContext.Session.SetString("Role", customer.Role.RoleName);
            }
               
           
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, customer.FullName),
        new Claim(ClaimTypes.Email, customer.Email),
        new Claim(ClaimTypes.Role, customer.Role.RoleName),
        new Claim("UserId", customer.UserId.ToString())
    };
            // 🔹 DEVICE ID
            string deviceId = DeviceIdHelper.GetOrCreateDeviceId(HttpContext);

            if (!string.IsNullOrEmpty(deviceId))
            {
                // 🔥 only guest cart items (UserId == null)
                var deviceCartItems = await _context.CartItems
                    .Where(c => c.DeviceId == deviceId && c.UserId == 0)
                    .ToListAsync();

                foreach (var item in deviceCartItems)
                {
                    // 🔍 check same product already exists for logged-in user
                    var userCartItem = await _context.CartItems.FirstOrDefaultAsync(c =>
                        c.UserId == customer.UserId &&
                        c.ProductId == item.ProductId
                    );

                    if (userCartItem != null)
                    {
                        // ➕ merge quantity
                        userCartItem.Quantity += item.Quantity;

                        // ❌ remove guest item
                        _context.CartItems.Remove(item);
                    }
                    else
                    {
                        // 🔁 just update UserId (NO NEW INSERT)
                        item.UserId = customer.UserId;
                        //item.CreatedAt = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();
            }



            //var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            //var principal = new ClaimsPrincipal(identity);

            //var authProperties = new AuthenticationProperties
            //{
            //    IsPersistent = true,      // Browser close ke baad bhi login rahe
            //    AllowRefresh = true       // Cookie refresh allow kare
            //};

            //await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
            //// -----------------------------------------------------
            //if (Url.IsLocalUrl(model.ReturnUrl))
            //{
            //    return Redirect(model.ReturnUrl);  
            //}
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,    
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties
            );

            // 👇 Redirect yaha hoga
            if (Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return RedirectToAction("Index", "WebHome"); 
        }

        private async Task SignInCustomer(Customers customer)
        {
            // ----------------- SESSION -----------------
            if (customer.CustType == "Customer")
            {
                HttpContext.Session.SetInt32("UserId", customer.UserId);
                HttpContext.Session.SetString("FullName", customer.FullName);
                HttpContext.Session.SetString("Email", customer.Email);
                HttpContext.Session.SetString("Role", customer.Role?.RoleName ?? "");
            }

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, customer.FullName ?? ""),
        new Claim(ClaimTypes.Email, customer.Email ?? ""),
        new Claim(ClaimTypes.Role, customer.Role?.RoleName ?? ""),
        new Claim("UserId", customer.UserId.ToString())
    };

            // 🔹 DEVICE ID CART MERGE
            string deviceId = DeviceIdHelper.GetOrCreateDeviceId(HttpContext);

            if (!string.IsNullOrEmpty(deviceId))
            {
                var deviceCartItems = await _context.CartItems
                    .Where(c => c.DeviceId == deviceId && c.UserId == 0)
                    .ToListAsync();

                foreach (var item in deviceCartItems)
                {
                    var userCartItem = await _context.CartItems.FirstOrDefaultAsync(c =>
                        c.UserId == customer.UserId &&
                        c.ProductId == item.ProductId
                    );

                    if (userCartItem != null)
                    {
                        userCartItem.Quantity += item.Quantity;
                        _context.CartItems.Remove(item);
                    }
                    else
                    {
                        item.UserId = customer.UserId;
                    }
                }

                await _context.SaveChangesAsync();
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties
            );
        }

        // ===================== LOGOUT =====================
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "WebHome");
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 8 || model.NewPassword.Length > 50)
                return BadRequest(new { success = false, message = "Invalid reset request." });

            var verifiedEmail = HttpContext.Session.GetString("PasswordResetVerifiedEmail");
            if (!string.Equals(verifiedEmail, model.Email, StringComparison.OrdinalIgnoreCase) ||
                !long.TryParse(HttpContext.Session.GetString("PasswordResetVerifiedUntil"), out var verifiedUntil) ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() > verifiedUntil)
                return BadRequest(new { success = false, message = "Verify your OTP first." });

            HttpContext.Session.Remove("PasswordResetVerifiedEmail");
            HttpContext.Session.Remove("PasswordResetVerifiedUntil");

            var user = await _context.Customers
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.Email == model.Email && x.IsActive && x.IsDeleted == 0 && x.CustType == "Customer");

            if (user == null)
                return Json(new { success = false, message = "User not found" });

            user.PasswordHash = CustomerPassword.Hash(model.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // AUTO LOGIN (same as Login method)
            await SignInCustomer(user);

            return Json(new
            {
                success = true,
                redirectUrl = Url.Action("Index", "WebHome")
            });
        }
    }
}
