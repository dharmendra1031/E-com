using System.Globalization;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = AdminStaffAccess.Roles)]
    public class ManageCustomerController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly AdminStaffAccess _access;

        public ManageCustomerController(EcommerceDbContext context, AdminStaffAccess access)
        {
            _context = context;
            _access = access;
        }
       
        [HttpGet]
        public async Task<IActionResult> CustomerList(int? status, string? startDate, string? endDate, int? customerId, string? name, string? mobile, int page = 1, int pageSize = 10, bool partial = false)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Customers, HttpContext.RequestAborted)).View) return Forbid();
            var ar = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var usersQuery = _context.Customers.AsNoTracking().Where(u => u.RoleId == 2 && u.IsDeleted == 0);
  
            if (status.HasValue)
                usersQuery = status.Value == 1 ? usersQuery.Where(u => u.IsActive) : usersQuery.Where(u => !u.IsActive);

       
            var formats = new[] { "yyyy-MM-dd", "dd-MM-yyyy" };
            bool hasStart = !string.IsNullOrWhiteSpace(startDate), hasEnd = !string.IsNullOrWhiteSpace(endDate);
            DateTime from = default, to = default;
            if ((hasStart && !DateTime.TryParseExact(startDate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out from)) ||
                (hasEnd && !DateTime.TryParseExact(endDate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out to)) ||
                (hasStart && hasEnd && from > to))
                return BadRequest(ar ? "نطاق التاريخ غير صالح." : "Invalid date range.");
            if (hasStart) usersQuery = usersQuery.Where(u => u.CreatedAt >= from);
            if (hasEnd && to < DateTime.MaxValue.Date)
            {
                var endExclusive = to.AddDays(1);
                usersQuery = usersQuery.Where(u => u.CreatedAt < endExclusive);
            }
            startDate = hasStart ? from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            endDate = hasEnd ? to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;

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
                FullName = ar && u.FullNameArb != null && u.FullNameArb != "" ? u.FullNameArb : u.FullName,
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
            return partial ? PartialView("_RegisterRows", pagedModel) : View(pagedModel);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetCustomerDetails(int id, int page = 1)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Customers, HttpContext.RequestAborted)).View) return Forbid();
            var ar = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var customer = _context.Customers.AsNoTracking()
                .Where(u => u.UserId == id && u.RoleId == 2 && u.IsDeleted == 0)
                .Select(u => new
                {
                    customerId = u.UserId,
                    fullName = ar && u.FullNameArb != null && u.FullNameArb != "" ? u.FullNameArb : u.FullName,
                    email = u.Email, phone = u.Phone, status = u.IsActive,
                    registrationDate = u.CreatedAt,
                    customerImage = u.ProfileImage == null ? null : "data:image/png;base64," + Convert.ToBase64String(u.ProfileImage)
                }).FirstOrDefault();
            if (customer == null) return NotFound();

            var addresses = (from ua in _context.UserAddresses.AsNoTracking()
                             join b in _context.Block on ua.Block equals b.Id.ToString() into blocks
                             from b in blocks.DefaultIfEmpty()
                             join a in _context.Area on ua.Area equals a.Id.ToString() into areas
                             from a in areas.DefaultIfEmpty()
                             where ua.UserId == id && ua.isdeleted == 0
                             orderby ua.IsDefault descending, ua.AddressId
                             select new
                             {
                                 title = ua.Title, addressHouseNo = ua.HouseNo, addressStreet = ua.Street,
                                 addressBlock = b == null ? ua.Block : (ar ? b.BlockNameArb : b.BlockName),
                                 addressArea = a == null ? ua.Area : (ar ? a.AreaNameArb : a.AreaName),
                                 addressFloor = ua.Floor, addressLandmark = ua.Landmark, isDefault = ua.IsDefault
                             }).ToList();

            // Legacy order register only. Marketplace orders remain in their dedicated workflows.
            var canViewOrders = (await _access.RightsAsync(User, AdminStaffAccess.Orders, HttpContext.RequestAborted)).View;
            var orders = _context.Orders.AsNoTracking().Where(o => canViewOrders && o.CustomerId == id && o.MarketplaceRequestId == null);
            const int pageSize = 10;
            int total = orders.Count();
            int pages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));
            page = Math.Clamp(page, 1, pages);
            var orderPage = orders.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.OrderId)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(o => new { orderId = o.OrderId, date = o.CreatedAt, totalAmount = o.TotalAmount,
                    status = ar ? o.OrderStatus.OrderStatusNameArb : o.OrderStatus.OrderstatusName,
                    paymentStatus = _context.PaymentStatus.Where(p => p.PaymentstatusId == o.PaymentStatus)
                        .Select(p => ar ? p.PaymentStatusNameArb : p.PaymentstatusName).FirstOrDefault() })
                .ToList();
            return Json(new { customer, addresses, orders = orderPage, page, pages, total, canViewOrders });
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetCustomerOrderItems(int id, int orderId, int page = 1)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Customers, HttpContext.RequestAborted)).View) return Forbid();
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Orders, HttpContext.RequestAborted)).View) return Forbid();
            if (!_context.Customers.Any(u => u.UserId == id && u.RoleId == 2 && u.IsDeleted == 0) ||
                !_context.Orders.Any(o => o.OrderId == orderId && o.CustomerId == id && o.MarketplaceRequestId == null))
                return NotFound();
            var ar = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
            var query = from ot in _context.OrderItems.AsNoTracking()
                        join p in _context.Products on ot.ProductId equals p.ProductId
                        where ot.OrderId == orderId
                        select new { productId = p.ProductId, productName = ar ? p.NameArb : p.Name,
                            quantity = ot.Quantity, price = ot.Price, totalPrice = ot.Quantity * ot.Price };
            const int pageSize = 25;
            int pages = Math.Max(1, (int)Math.Ceiling((double)query.Count() / pageSize));
            page = Math.Clamp(page, 1, pages);
            var items = query.OrderBy(x => x.productId).ThenBy(x => x.price).ThenBy(x => x.quantity)
                .Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Json(new { items, page, pages });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult ToggleUserStatus(int id, bool? active)
        {
            if (!ModelState.IsValid || !active.HasValue)
                return Json(new { success = false, message = "Invalid customer status." });
            var user = _context.Customers.FirstOrDefault(u => u.UserId == id && u.RoleId == 2 && u.IsDeleted == 0);
            if (user == null)
                return Json(new { success = false, message = "User not found." });
            user.IsActive = active.Value;
            _context.SaveChanges();
            return Json(new { success = true, active = user.IsActive });
        }

        [HttpGet]
        public async Task<IActionResult> AutoCompleteSql(string term)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Customers, HttpContext.RequestAborted)).View) return Forbid();
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            IQueryable<Customers> queryable = _context.Customers.AsNoTracking().Where(u => u.RoleId == 2 && u.IsDeleted == 0);

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
                .Distinct().OrderBy(n => n).Take(10)
                .ToListAsync();

            return Json(suggestions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult ChangePassword([FromBody] ChangePasswordModel model)
        {
            if (model == null || !CustomerPassword.IsValidNew(model.NewPassword))
                return Json(new { success = false, message = "Password is required" });

            var user = _context.Customers.FirstOrDefault(x => x.UserId == model.UserId && x.RoleId == 2 && x.IsDeleted == 0 && x.CustType == "Customer");

            if (user == null)
                return Json(new { success = false, message = "User not found" });

            user.PasswordHash = CustomerPassword.Hash(model.NewPassword);

            user.UpdatedAt = DateTime.UtcNow;

            _context.SaveChanges();

            return Json(new { success = true });
        }
    }
}
