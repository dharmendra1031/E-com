using DocumentFormat.OpenXml.Drawing.Charts;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = "Customer")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class UserProfileController : Controller
    {
        private readonly EcommerceDbContext _context;
        public UserProfileController(EcommerceDbContext context)
        {
            _context = context;
        }
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue || User.FindFirst("UserId")?.Value != userId.Value.ToString())
                context.Result = RedirectToAction("Login", "UserAccount", new { returnUrl = "/UserProfile/Profile" });
            base.OnActionExecuting(context);
        }
        public IActionResult Profile()
        {
            int userId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            var userWithAddresses = (from u in _context.Customers
                                     join a in _context.UserAddresses
                                     on u.UserId equals a.UserId into addresses
                                     where u.UserId == userId 
                                     select new
                                     {
                                         User = u,
                                         Addresses = addresses.Where(x => x.isdeleted == 0).ToList()
                                     }).FirstOrDefault();

            if (userWithAddresses == null) return NotFound();

            ViewBag.User = userWithAddresses.User;
            ViewBag.Addresses = userWithAddresses.Addresses;

            return View();
        }


        //public IActionResult ManageAddressesPartial()
        //{

        //    var userId = HttpContext.Session.GetInt32("UserId");

        //    var addresses = _context.UserAddresses
        //                    .Where(a => a.UserId == userId && a.IsActive == 0)
        //                    .ToList();

        //    return PartialView("_ManageAddressesPartial", addresses);
        //}
        public IActionResult ManageAddressesPartial()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            var userId = HttpContext.Session.GetInt32("UserId");

            var addresses = (
                from ua in _context.UserAddresses

                join ar in _context.Area
                    on ua.Area equals ar.Id.ToString() into areaJoin
                from ar in areaJoin.DefaultIfEmpty()

                join bl in _context.Block
                    on ua.Block equals bl.Id.ToString() into blockJoin
                from bl in blockJoin.DefaultIfEmpty()

                where ua.UserId == userId && ua.isdeleted == 0

                select new UserAddressViewModel
                {
                    AddressId = ua.AddressId,
                    Title = ua.Title,
                    HouseNo = ua.HouseNo,
                    Street = ua.Street,
                    Floor = ua.Floor,
                    Landmark = ua.Landmark,
                    Phone = ua.Phone,
                    IsDefault = ua.IsDefault,
                    AreaName = isArabic ? ar.AreaNameArb : ar.AreaName,
                    BlockName = isArabic ? bl.BlockNameArb : bl.BlockName
                }

            ).ToList();

            return PartialView("_ManageAddressesPartial", addresses);
        }

        //public IActionResult AddAddressPartial()
        //{

        //    return PartialView("_AddAddressPartial", new UserAddressViewModel());
        //}
        public IActionResult AddAddressPartial()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            var userId = HttpContext.Session.GetInt32("UserId");

            var customer = _context.Customers
                            .FirstOrDefault(x => x.UserId == userId);

            ViewBag.AreaList = _context.Area
                                .Select(a => new
                                {
                                    a.Id,
                                    AreaName = isArabic ? a.AreaNameArb : a.AreaName
                                }).ToList();

            var model = new UserAddressViewModel();

            if (customer != null)
            {
                model.Phone = customer.Phone;  
            }

            return PartialView("_AddAddressPartial", model);
        }

        [HttpGet]
        public IActionResult GetAddressById(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null || User.Identity?.IsAuthenticated != true ||
                User.FindFirst("UserId")?.Value != userId.Value.ToString() || User.IsInRole("Admin"))
                return Unauthorized();

            var addr = _context.UserAddresses.FirstOrDefault(x => x.AddressId == id && x.UserId == userId.Value && x.isdeleted == 0);

            if (addr == null)
                return Json(new { success = false, message = "Address not found" });

            return Json(new
            {
                success = true,
                data = addr
            });
        }

        [HttpPost]
        public IActionResult AddAddress(UserAddress model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return Json(new { success = false, message = "User not logged in." });

            // 🔴 Required fields validation
            if (string.IsNullOrWhiteSpace(model.Area) ||
                string.IsNullOrWhiteSpace(model.Block) ||
                string.IsNullOrWhiteSpace(model.Street) ||
                string.IsNullOrWhiteSpace(model.Phone))
            {
                return Json(new
                {
                    success = false,
                    message = "Must be Required"
                });
            }

         
            model.Title ??= "";
            model.HouseNo ??= "";
            model.Floor ??= "";
            model.Landmark ??= "";
            model.Governorate ??= "";

            // 🔁 ADD or UPDATE
            if (model.AddressId > 0)
            {
                // 🔹 UPDATE
                var dbAddress = _context.UserAddresses
                    .FirstOrDefault(x => x.AddressId == model.AddressId && x.UserId == userId.Value);

                if (dbAddress == null)
                    return Json(new { success = false, message = "Address not found." });

                dbAddress.Title = model.Title;
                dbAddress.Area = model.Area;
                dbAddress.Block = model.Block;
                dbAddress.Street = model.Street;
                dbAddress.HouseNo = model.HouseNo;
                dbAddress.Floor = model.Floor;
                dbAddress.Landmark = model.Landmark;
                dbAddress.Phone = model.Phone;
                dbAddress.Governorate = model.Governorate;

                _context.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Address updated successfully"
                });
            }
            else
            {
                // ➕ ADD
                model.UserId = userId.Value;
                model.CreatedAt = DateTime.Now;
                model.isdeleted = 0;
                _context.UserAddresses.Add(model);
                _context.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Address added successfully"
                });
            }
        }

        public JsonResult GetBlocksByArea(int areaId)
        {
            bool ar = System.Globalization.CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var blocks = _context.Block
                            .Where(b => b.AreaId == areaId)
                            .Select(b => new
                            {
                                b.Id,
                                BlockName = ar ? b.BlockNameArb : b.BlockName
                            }).ToList();

            return Json(blocks);
        }



        public IActionResult MyOrdersPartial(int page = 1)
        {
            const int pageSize = 10;
            var userId = HttpContext.Session.GetInt32("UserId");
            bool ar = System.Globalization.CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var query = from o in _context.Orders.AsNoTracking()
                        join os in _context.OrderStatus on o.Status equals os.OrderstatusId
                        join ps in _context.PaymentStatus on o.PaymentStatus equals ps.PaymentstatusId
                        where o.CustomerId == userId
                        orderby o.CreatedAt descending, o.OrderId descending
                        select new OrderprofileViewModel
                        {
                            OrderId = o.OrderId, CreatedAt = o.CreatedAt,
                            OrderStatusName = ar ? os.OrderStatusNameArb : os.OrderstatusName,
                            PaymentStatusName = ar ? ps.PaymentStatusNameArb : ps.PaymentstatusName,
                            TotalAmount = o.TotalAmount, paytype = o.PaymentType
                        };
            int total = query.Count();
            int pages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));
            page = Math.Clamp(page, 1, pages);
            ViewBag.Page = page;
            ViewBag.TotalPages = pages;
            return PartialView("_MyOrdersPartial", query.Skip((page - 1) * pageSize).Take(pageSize).ToList());
        }

        public IActionResult PersonalInformation()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var data = (from u in _context.Customers
                        join a in _context.UserAddresses
                        on u.UserId equals a.UserId into addresses
                        where u.UserId == userId
                        select new UserProfileViewModel
                        {
                            User = u,
                            Addresses = addresses.ToList()
                        }).FirstOrDefault();

            if (data == null)
                return PartialView("_PersonalInformation", new UserProfileViewModel());

            return PartialView("_PersonalInformation", data);
        }



        // Load Change Password Partial
        public IActionResult ChangePasswordPartial()
        {
            return PartialView("_ChangePasswordPartial");
        }

        // Update Password POST
        [HttpPost]
        public IActionResult ChangePassword(string CurrentPassword, string NewPassword, string ConfirmPassword)
        {
            int userId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value);

            var customer = _context.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer == null)
                return Json(new { success = false, message = "User not found." });

            // Validate current password
            if (customer.PasswordHash != CurrentPassword) // Simple example; hash check if used
                return Json(new { success = false, message = "Current password is incorrect." });

            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8 || NewPassword.Length > 50)
                return Json(new { success = false, message = "Use 8–50 characters." });

            if (NewPassword != ConfirmPassword)
                return Json(new { success = false, message = "New password and confirmation do not match." });

            customer.PasswordHash = NewPassword; // Hash it in real app!
            _context.SaveChanges();

            return Json(new { success = true, message = "Password updated successfully!" });
        }


        [HttpPost]
        public IActionResult DeleteAddress(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null || User.Identity?.IsAuthenticated != true ||
                User.FindFirst("UserId")?.Value != userId.Value.ToString() || User.IsInRole("Admin"))
                return Unauthorized();

            var address = _context.UserAddresses.FirstOrDefault(x => x.AddressId == id && x.UserId == userId.Value && x.isdeleted == 0);

            if (address == null)
                return Json(new { success = false, message = "Address not found" });

            address.isdeleted = 1;

            _context.SaveChanges();

            return Json(new { success = true, message = "Address deleted successfully" });
        }
     


        [HttpPost]
        public IActionResult UpdateProfile(Customers model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Json(new { success = false, message = "Not logged in" });

            var user = _context.Customers.FirstOrDefault(x => x.UserId == userId);
            if (user == null)
                return Json(new { success = false, message = "User not found" });

            if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Phone) ||
                !System.Text.RegularExpressions.Regex.IsMatch(model.Phone, @"^(\+965)?\d{8}$") || model.BirthDate > DateTime.Today)
                return Json(new { success = false, message = "Check your profile details." });

            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.BirthDate = model.BirthDate;

            _context.SaveChanges();

            return Json(new { success = true, message = "Profile updated successfully" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SoftDeleteUser()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var user = _context.Customers.FirstOrDefault(u => u.UserId == userId && u.IsDeleted == 0);

            if (user == null)
            {
                //return NotFound(new { success = false, message = "User not found." });
                return RedirectToAction("Login", "UserAccount");
            }

            if (user.IsDeleted == 1)
            {
                return BadRequest(new { success = false, message = "User Not Found." });
            }

            user.IsDeleted = 1;
            user.PasswordHash = "0";
            _context.Customers.Update(user);
            _context.SaveChanges();

            return Ok(new { success = true, message = "User deleted successfully." });
        }


    }

}
