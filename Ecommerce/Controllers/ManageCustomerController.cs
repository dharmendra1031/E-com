using System.Globalization;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    [Authorize]
    public class ManageCustomerController : Controller
    {
        private readonly EcommerceDbContext _context;

        public ManageCustomerController(EcommerceDbContext context)
        {
            _context = context;
        }
       
        [HttpGet]
        public IActionResult CustomerList(int? status, string startDate, string endDate, int? customerId, string name, string mobile, int page = 1, int pageSize = 10)
        {
            var usersQuery = _context.Customers.Where(u => u.RoleId == 2 && u.IsDeleted == 0).AsQueryable();
  
            if (status.HasValue)
                usersQuery = status.Value == 1 ? usersQuery.Where(u => u.IsActive) : usersQuery.Where(u => !u.IsActive);

       
            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
            {
                if (DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStartDate) &&
                    DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedEndDate))
                {
                    parsedEndDate = parsedEndDate.Date.AddDays(1).AddTicks(-1);
                    usersQuery = usersQuery.Where(u => u.CreatedAt >= parsedStartDate && u.CreatedAt <= parsedEndDate);
                }
            }
        
            if (customerId.HasValue)
                usersQuery = usersQuery.Where(u => u.UserId == customerId.Value);

           
            if (!string.IsNullOrEmpty(name))
                usersQuery = usersQuery.Where(u => u.FullName.Contains(name) || u.FullNameArb.Contains(name));

           
            if (!string.IsNullOrEmpty(mobile))
                usersQuery = usersQuery.Where(u => u.Phone.Contains(mobile));

           
            pageSize = Math.Clamp(pageSize, 1, 100);
            int totalItems = usersQuery.Count();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize)));
            usersQuery = usersQuery.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.UserId);

           
            var pagedData = usersQuery.Skip((page - 1) * pageSize).Take(pageSize).Select(u => new CustomerGridViewModel
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            }).ToList();


            var pagedModel = new PagedResult<CustomerGridViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
            ViewBag.FilterStatus = status;
            ViewBag.FilterStartDate = startDate;
            ViewBag.FilterEndDate = endDate;
            ViewBag.FilterCustomerId = customerId;
            ViewBag.FilterName = name;
            ViewBag.FilterMobile = mobile;
            return View(pagedModel);
        }

        public IActionResult GetCustomerDetails(int id)
        {
            var customer = _context.Customers.FirstOrDefault(u => u.UserId == id && u.IsDeleted == 0);

            if (customer == null)
                return Json(new { customer = (object)null });

            // ✅ Customer ke addresses
            //var userAddresses = _context.UserAddresses
            //    .Where(u => u.UserId == id)
            //    .ToList();

            //var addressList = userAddresses.Any()
            //    ? userAddresses.Select(a => new
            //    {
            //        title = a.Title,
            //        addressHouseNo = a.HouseNo,
            //        addressStreet = a.Street,
            //        addressBlock = a.Block,
            //        addressArea = a.Area,
            //        addressGovernorate = a.Governorate
            //    }).ToList()
            //    : null;

            var addressList = (from ua in _context.UserAddresses
                               join b in _context.Block on ua.Block equals b.Id.ToString()
                               join a in _context.Area on ua.Area equals a.Id.ToString()
                               where ua.UserId == id
                               select new
                               {
                                   title = ua.Title,
                                   addressHouseNo = ua.HouseNo,
                                   addressStreet = ua.Street,
                                   addressBlock = b.BlockName, 
                                   addressArea = a.AreaName,     
                                   addressFloor = ua.Floor,
                                   addressLandmark = ua.Landmark
                               }).ToList();
            // ✅ Orders + Products
            var orderItems = (from ot in _context.OrderItems
                              join o in _context.Orders on ot.OrderId equals o.OrderId
                              join p in _context.Products on ot.ProductId equals p.ProductId
                              where o.CustomerId == id
                              select new
                              {
                                  orderId = o.OrderId,
                                  productName = p.Name,
                                  quantity = ot.Quantity,
                                  price = ot.Price,
                                  totalPrice = ot.Quantity * ot.Price
                              }).ToList();

            // ✅ Profile Image
            string customerImageBase64 = null;
            if (customer.ProfileImage != null)
            {
                customerImageBase64 = "data:image/png;base64," + Convert.ToBase64String(customer.ProfileImage);
            }

            // ✅ Final return
            return Json(new
            {
                customer = new
                {
                    fullName = customer.FullName,
                    email = customer.Email,
                    phone = customer.Phone,
                    status = customer.IsActive,
                    customerImage = customerImageBase64,
                    customerId = customer.UserId,
                    registrationDate = customer.CreatedAt.ToString("dd-MM-yyyy")
                },
                addresses = addressList,
                orderItems = orderItems
            });
        }


        [HttpPost]
        public IActionResult ToggleUserStatus(int id)
        {
            var user = _context.Customers.FirstOrDefault(u => u.UserId == id && u.IsDeleted == 0);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }
            user.IsActive = !user.IsActive;
            _context.SaveChanges();

            return Json(new { success = true });
        }
        [HttpGet]
        public async Task<JsonResult> AutoCompleteSql(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            IQueryable<Customers> queryable = _context.Customers;

            // Detect Arabic characters
            bool isArabic = term.Any(c => c >= 0x0600 && c <= 0x06FF);

            if (isArabic)
            {
                queryable = queryable.Where(c => c.FullNameArb.Contains(term));
            }
            else
            {
                queryable = queryable.Where(c => c.FullName.Contains(term));
            }

            var suggestions = await queryable
                .Select(c => isArabic ? c.FullNameArb : c.FullName)
                .Take(10)
                .ToListAsync();

            return Json(suggestions);
        }

        [HttpPost]
        public IActionResult ChangePassword([FromBody] ChangePasswordModel model)
        {
            if (string.IsNullOrWhiteSpace(model.NewPassword))
                return Json(new { success = false, message = "Password is required" });

            var user = _context.Customers.FirstOrDefault(x => x.UserId == model.UserId);

            if (user == null)
                return Json(new { success = false, message = "User not found" });

            // ✅ Hash password before saving
            //var hasher = new PasswordHasher<Customers>();
            //user.PasswordHash = hasher.HashPassword(user, model.NewPassword);

            user.PasswordHash = model.NewPassword;

            user.UpdatedAt = DateTime.UtcNow;

            _context.SaveChanges();

            return Json(new { success = true });
        }
    }
}
