using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = AdminStaffAccess.Roles)]
    public class ManageOrdersController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly AdminStaffAccess _access;

        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private bool MarketplaceOrdersEnabled => _environment.IsDevelopment() &&
            _configuration.GetValue("Marketplace:VendorMasterEnabled",false) &&
            _configuration.GetValue("Marketplace:VendorOrdersEnabled",false);
        private bool MarketplaceFulfillmentEnabled => MarketplaceOrdersEnabled &&
            _configuration.GetValue("Marketplace:VendorFulfillmentEnabled",false);

        public ManageOrdersController(EcommerceDbContext context, IConfiguration configuration, AdminStaffAccess access, IWebHostEnvironment environment)
        {
            _context = context;
            _access = access;
            _configuration = configuration;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> PendingOrder(int? status, string? startDate, string? paymenttype, string? endDate, int? paymentstatus, int page = 1, int pageSize = 10, bool partial = false)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Orders, HttpContext.RequestAborted)).View) return Forbid();
            ViewBag.MarketplaceOrdersEnabled = MarketplaceOrdersEnabled;
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;

            var query = from o in _context.Orders
                        join u in _context.Customers on o.CustomerId equals u.UserId
                        join p in _context.PaymentStatus on o.PaymentStatus equals p.PaymentstatusId
                        join or in _context.OrderStatus on o.Status equals or.OrderstatusId
                        select new OrderItemViewModel
                        {
                            OrderId = o.OrderId,
                            FullName = isArabic ? u.FullNameArb : u.FullName,
                            TotalPrice = o.TotalAmount,
                            PaymentStatus = isArabic ? p.PaymentStatusNameArb : p.PaymentstatusName,
                            OrderStatus = isArabic ? or.OrderStatusNameArb : or.OrderstatusName,
                            Orderdate = o.CreatedAt,
                            OrderStatusId = o.Status,
                            PaymentStatusId = p.PaymentstatusId,
                            Paytype = o.PaymentType
                        };

            // Filter by Order Status
            if (status.HasValue)
            {
                query = query.Where(x => x.OrderStatusId == status.Value);
            }

            // Filter by Payment Status
            if (paymentstatus.HasValue)
            {
                query = query.Where(x => x.PaymentStatusId == paymentstatus.Value);
            }
            if (!string.IsNullOrEmpty(paymenttype))
            {
                query = query.Where(x => x.Paytype == paymenttype);
            }



            // Filter by Date Range using dd-MM-yyyy format
            var formats = new[] { "yyyy-MM-dd", "dd-MM-yyyy" };
            var hasStart = !string.IsNullOrWhiteSpace(startDate);
            var hasEnd = !string.IsNullOrWhiteSpace(endDate);
            DateTime from = default, to = default;
            if ((hasStart && !DateTime.TryParseExact(startDate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out from)) ||
                (hasEnd && !DateTime.TryParseExact(endDate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out to)) ||
                (hasStart && hasEnd && from > to))
                return BadRequest(isArabic ? "نطاق التاريخ غير صالح." : "Invalid date range.");
            if (hasStart) query = query.Where(x => x.Orderdate >= from);
            if (hasEnd && to < DateTime.MaxValue.Date)
            {
                var endExclusive = to.AddDays(1);
                query = query.Where(x => x.Orderdate < endExclusive);
            }
            startDate = hasStart ? from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            endDate = hasEnd ? to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;

            // Sort descending by Order Date
            query = query.OrderByDescending(x => x.Orderdate).ThenByDescending(x => x.OrderId);


            ViewBag.StatusList = _context.OrderStatus
                .Select(s => new SelectListItem
                {
                    Value = s.OrderstatusId.ToString(),
                    Text = isArabic ? s.OrderStatusNameArb : s.OrderstatusName,
                    Selected = (status.HasValue && s.OrderstatusId == status.Value)
                }).ToList();

            ViewBag.Selectedpaymentstatus = _context.PaymentStatus
                .Select(s => new SelectListItem
                {
                    Value = s.PaymentstatusId.ToString(),
                    Text = isArabic ? s.PaymentStatusNameArb : s.PaymentstatusName,
                    Selected = (paymentstatus.HasValue && s.PaymentstatusId == paymentstatus.Value)
                }).ToList();

            pageSize = Math.Clamp(pageSize, 1, 100);
            int totalItems = query.Count();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize)));
            var pagedData = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedModel = new PagedResult<OrderItemViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
            // Keep raw values to refill input
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentPaymentStatus = paymentstatus;
            ViewBag.CurrentPaymentType = paymenttype;
            ViewBag.CurrentStartDate = startDate;
            ViewBag.CurrentEndDate = endDate;

            return partial ? PartialView("_OrderRegister", pagedModel) : View(pagedModel);
        }


        [HttpGet]
        public async Task<IActionResult> MarketplaceOrders(int? id, string? status, int page=1, CancellationToken ct=default)
        {
            if(!MarketplaceOrdersEnabled) return NotFound();
            if(!(await _access.RightsAsync(User,AdminStaffAccess.Orders,ct)).View) return Forbid();
            if(id is <=0) return BadRequest();
            page=Math.Clamp(page,1,100000);
            if(!VendorOrderQueries.ValidStatus(status)) status=null;
            var rows=await VendorOrderQueries.ReadPageAsync(_context,id,status,page,ct);
            ViewBag.VendorId=id; ViewBag.Page=page; ViewBag.HasNext=rows.Count>25; ViewBag.Status=status;
            ViewData["AdminOrders"]=true;
            return View(rows.Take(25).ToList());
        }

        [HttpGet]
        public async Task<IActionResult> MarketplaceOrderDetails(int id,int vendorOrderId,CancellationToken ct)
        {
            if(!MarketplaceOrdersEnabled) return NotFound();
            var rights=await _access.RightsAsync(User,AdminStaffAccess.Orders,ct);
            if(!rights.View) return Forbid();
            var rows=await VendorOrderQueries.ReadLinesAsync(_context,id,vendorOrderId,ct);
            if(rows.Count==0) return NotFound();
            ViewData["AdminOrders"]=true;
            ViewBag.VendorId=id; ViewBag.VendorOrderId=vendorOrderId;
        ViewBag.FulfillmentHistory=await VendorOrderQueries.ReadHistoryAsync(_context,id,vendorOrderId,ct);
            ViewBag.CanAdvance=MarketplaceFulfillmentEnabled && rights.Edit;
            return View("~/Views/VendorPortal/OrderDetails.cshtml",rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdvanceMarketplaceFulfillment(int id,int vendorOrderId,string expectedStatus,
            string newStatus,bool deliveryConfirmed,CancellationToken ct)
        {
            if(!MarketplaceFulfillmentEnabled) return NotFound();
            if(!(await _access.RightsAsync(User,AdminStaffAccess.Orders,ct)).Edit ||
                !int.TryParse(User.FindFirst("UserId")?.Value,out var actor)) return Forbid();
            if(!ModelState.IsValid || !VendorOrderQueries.CanAdvance(expectedStatus,newStatus,true,deliveryConfirmed))
                return BadRequest();
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    EXEC dbo.AdvanceVendorFulfillment @VendorId={id},@VendorOrderId={vendorOrderId},
                        @ActorId={actor},@ExpectedStatus={expectedStatus},@NewStatus={newStatus},
                        @AdminPortal=1,@DeliveryConfirmed={deliveryConfirmed}
                    """,ct);
                TempData["FulfillmentMessage"]="Order status saved. / تم حفظ حالة الطلب.";
            }
            catch(SqlException ex) when(ex.Number==52001) { return Forbid(); }
            catch(SqlException ex) when(ex.Number is 52000 or 52002 or 52003)
            {
                TempData["FulfillmentMessage"]="Order status not changed. Refresh and review. / لم تتغير حالة الطلب.";
            }
            return RedirectToAction(nameof(MarketplaceOrderDetails),new {id,vendorOrderId});
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult GetStatusList(int orderId)
        {
            var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null || order.MarketplaceRequestId.HasValue) return Conflict();
            var currentStatusId = order?.Status ?? 0;
            var ar = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;

            var statusList = _context.OrderStatus
                .Select(s => new SelectListItem
                {
                    Value = s.OrderstatusId.ToString(),
                    Text = ar ? s.OrderStatusNameArb : s.OrderstatusName,
                    Selected = (s.OrderstatusId == currentStatusId)
                }).ToList();

            return Json(statusList);
        }
        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Orders, HttpContext.RequestAborted)).View) return Forbid();
            try
            {
                var culture = System.Globalization.CultureInfo.CurrentCulture;
                bool isArabic = culture.TextInfo.IsRightToLeft;

                var order = (from o in _context.Orders
                             join u in _context.Customers on o.CustomerId equals u.UserId
                             join st in _context.OrderStatus on o.Status equals st.OrderstatusId
                             join pyt in _context.PaymentStatus on o.PaymentStatus equals pyt.PaymentstatusId
                             join ad in _context.UserAddresses on o.DeliveryAddress equals ad.AddressId
                             join ar in _context.Area on ad.Area equals ar.Id.ToString() into areaGroup
                             from ar in areaGroup.DefaultIfEmpty()
                             join b in _context.Block on ad.Block equals b.Id.ToString() into blockGroup
                             from b in blockGroup.DefaultIfEmpty()
                             where o.OrderId == id && u.IsDeleted==0
                             select new OrderDetailModel
                             {
                                 OrderId = o.OrderId,
                                 UserId = o.CustomerId,
                                 ShareholderNo = u.ShareholderNo,
                                 //TotalAmount = (o.TotalAmount ?? 0) + (o.DeliveryCharge ?? 0),
                                 TotalAmount = o.TotalAmount,

                                 Status = isArabic ? st.OrderStatusNameArb : st.OrderstatusName,
                                 PaymentStatus = isArabic ? pyt.PaymentStatusNameArb : pyt.PaymentstatusName,
                                 PaymentType = o.PaymentType.ToString(),
                                 CouponCode = o.CouponCode,
                                 DiscountAmount = o.DiscountAmount,
                                 CreatedAt = o.CreatedAt,
                                 FullName = isArabic ? u.FullNameArb : u.FullName,

                                 Price = _context.OrderItems
                                            .Where(x => x.OrderId == o.OrderId)
                                            .Sum(x => (decimal?)(x.Price ?? 0) * (x.Quantity ?? 0)) ?? 0,

                                 Vatpersent = o.VatPercent,
                                 VatAmount = o.VatAmount,

                                 House = ad != null ? ad.HouseNo : "",
                                 Street = ad != null ? ad.Street : "",
                                 //Block = ad != null ? ad.Block : "",
                                 //Area = ad != null ? ad.Area : "",
                                 Block = b != null
                     ? (isArabic ? b.BlockNameArb : b.BlockName)
                     : (ad.Block ?? ""),
                                 Area = ar != null
                     ? (isArabic ? ar.AreaNameArb : ar.AreaName)
                     : (ad.Area ?? ""),
                                 Landmark = ad != null ? ad.Landmark : "",
                                 Floor = ad != null ? ad.Floor : "",
                                 Governorate = ad != null ? ad.Governorate : "",

                                 Email = u.Email,
                                 Phone = u.Phone,
                                 DeliveryCharge = o.DeliveryCharge,

                                 Items = (from ot in _context.OrderItems
                                          join p in _context.Products on ot.ProductId equals p.ProductId
                                          where ot.OrderId == o.OrderId
                                          select new OrderItemDetailViewModel
                                          {
                                              ProductName = isArabic ? p.NameArb : p.Name,
                                              Quantity = ot.Quantity ?? 0,
                                              Price = ot.Price ?? 0
                                          }).ToList()
                             }).FirstOrDefault();

                if (order == null)
                    return Json(new { success = false, message = "Order not found" });

                return Json(new { success = true, data = order });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Something went wrong while fetching order details.",
                    error = ex.Message
                });
            }
        }

        // Re-authentication is scoped to the current administrator, order and operation.
        private string VerificationKey(int userId, int orderId, string operation)
            => $"OrderVerification:{userId}:{orderId}:{operation}";

        private bool ConsumeVerification(int orderId, string operation, out int userId)
        {
            if (!int.TryParse(User.FindFirst("UserId")?.Value, out userId))
                return false;
            var key = VerificationKey(userId, orderId, operation);
            var proof = HttpContext.Session.GetString(key);
            HttpContext.Session.Remove(key);
            var currentUserId = userId;
            return long.TryParse(proof, out var expires) && expires >= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                && _context.Users.Any(u => u.UserId == currentUserId && u.IsActive && u.Role.IsActive && u.Role.RoleName == "Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public JsonResult VerifyPassword([FromBody] PasswordRequest model)
        {
            if (model == null || string.IsNullOrEmpty(model.Password)
                || !int.TryParse(User.FindFirst("UserId")?.Value, out var userId)
                || model.OrderId <= 0 || (model.Operation != "order" && model.Operation != "payment"))
                return Json(new { valid = false });

            var key = VerificationKey(userId, model.OrderId, model.Operation);
            HttpContext.Session.Remove(key);
            var user = _context.Users.AsNoTracking().FirstOrDefault(u => u.UserId == userId && u.IsActive && u.Role.IsActive && u.Role.RoleName == "Admin");
            var allowLegacy = _configuration.GetValue("Authentication:AllowLegacyStaffPasswords", false);
            if (user == null || !StaffPassword.Verify(user.PasswordHash, model.Password, allowLegacy, out _)
                || !_context.Orders.Any(o => o.OrderId == model.OrderId && o.MarketplaceRequestId == null &&
                    (model.Operation != "payment" || o.PaymentType == "cod")))
                return Json(new { valid = false });

            HttpContext.Session.SetString(key, DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            return Json(new { valid = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateOrderStatus([FromBody] ChangeStatusRequest model)
        {
            if (model == null || !ModelState.IsValid || !ConsumeVerification(model.OrderId, "order", out var userId))
                return Json(new { success = false, message = "Verification required. Reopen the order and verify your password." });
            using var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var order = _context.Orders.IgnoreQueryFilters().FirstOrDefault(o => o.OrderId == model.OrderId);
            if (order?.MarketplaceRequestId != null)
                return Conflict(new { success = false, message = "Use marketplace fulfillment to update this order." });
            if (order == null || !_context.OrderStatus.Any(x => x.OrderstatusId == model.CurrentStatus))
                return Json(new { success = false, message = "Order or status not found." });
            if (order.Status == model.CurrentStatus)
                return Json(new { success = true, message = "Order status is already up to date." });

            var previousStatus = order.Status;
            order.Status = model.CurrentStatus;
            order.UpdatedAt = DateTime.Now;
            _context.ChangeOrderStatus.Add(new ChangeOrderStatus
            {
                OrderId = model.OrderId,
                PreviousStatus = previousStatus,
                CurrentStatus = model.CurrentStatus,
                Remark = string.IsNullOrWhiteSpace(model.Remark) ? null : model.Remark,
                CreatedAt = DateTime.Now,
                CreatedBy = userId
            });
            _context.SaveChanges();
            transaction.Commit();
            return Json(new { success = true, message = "Order status updated successfully." });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult PaymentstatusList(int orderId)
        {
            var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null || order.MarketplaceRequestId.HasValue || !string.Equals(order.PaymentType, "cod", StringComparison.OrdinalIgnoreCase)) return Conflict();
            var paymentstatusId = order?.PaymentStatus ?? 0;
            var ar = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;

            var statusList = _context.PaymentStatus
                .Select(s => new SelectListItem
                {
                    Value = s.PaymentstatusId.ToString(),
                    Text = ar ? s.PaymentStatusNameArb : s.PaymentstatusName,
                    Selected = (s.PaymentstatusId == paymentstatusId)
                }).ToList();

            return Json(statusList);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public JsonResult UpdatePaymentStatus(int orderId, int PaymentStatus)
        {
            if (!ModelState.IsValid || !ConsumeVerification(orderId, "payment", out _))
                return Json(new { success = false, message = "Verification required. Reopen the order and verify your password." });
            try
            {
                using var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
                var order = _context.Orders.IgnoreQueryFilters().FirstOrDefault(o => o.OrderId == orderId);
                if (order?.MarketplaceRequestId != null || !string.Equals(order?.PaymentType, "cod", StringComparison.OrdinalIgnoreCase))
                    return Json(new { success = false, message = "Online and marketplace payments require gateway verification." });
                var payments = _context.Payments.Where(p => p.OrderId == orderId).Take(2).ToList();
                if (order == null || payments.Count != 1 || !_context.PaymentStatus.Any(x => x.PaymentstatusId == PaymentStatus))
                    return Json(new { success = false, message = "Order, payment record or status is invalid. Review the payment records." });

                var payment = payments[0];
                if (payment.PaymentStatus != PaymentStatus && PaymentStatus == 1)
                    payment.PaidAt = DateTime.Now;
                order.PaymentStatus = PaymentStatus;
                order.UpdatedAt = DateTime.Now;
                payment.PaymentStatus = PaymentStatus;
                _context.SaveChanges();
                transaction.Commit();
                return Json(new { success = true, message = "Payment status updated successfully." });
            }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<ManageOrdersController>>().LogError(ex, "Payment status update failed for order {OrderId}", orderId);
                return Json(new { success = false, message = "Unable to update payment status." });
            }
        }

        public class ChangeStatusRequest
        {
            public int OrderId { get; set; }
            public int CurrentStatus { get; set; }
            public string? Remark { get; set; }
        }
        public class PasswordRequest
        {
            public string? Password { get; set; }
            public int OrderId { get; set; }
            public string Operation { get; set; } = "";
        }


        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult GetInvoice(int orderId)
        {
            try
            {
                var culture = System.Globalization.CultureInfo.CurrentCulture;
                bool isArabic = culture.TextInfo.IsRightToLeft;

                // ✅ Get company details (assume only 1 record)
                var company = _context.CompanyDetails.FirstOrDefault();

                // ✅ Get order and related data
                var order = (from o in _context.Orders
                             join u in _context.Customers on o.CustomerId equals u.UserId
                             join st in _context.OrderStatus on o.Status equals st.OrderstatusId
                             join pyt in _context.PaymentStatus on o.PaymentStatus equals pyt.PaymentstatusId
                             where o.OrderId == orderId && u.IsDeleted==0
                             select new
                             {
                                 o.OrderId,
                                 o.CreatedAt,
                                 o.PaymentType,
                                 o.CouponCode,
                                 o.DiscountAmount,
                                 o.VatPercent,
                                 o.VatAmount,
                                 o.TotalAmount,
                                 Status = isArabic ? st.OrderStatusNameArb : st.OrderstatusName,
                                 PaymentStatus = isArabic ? pyt.PaymentStatusNameArb : pyt.PaymentstatusName,
                                 User = new
                                 {
                                     u.UserId,
                                     FullName = isArabic ? u.FullNameArb : u.FullName,
                                     u.Email,
                                     u.Phone,
                                     u.ShareholderNo
                                 },
                                 Items = (from ot in _context.OrderItems
                                          join p in _context.Products on ot.ProductId equals p.ProductId
                                          where ot.OrderId == o.OrderId
                                          select new
                                          {
                                              ProductName = isArabic ? p.NameArb : p.Name,
                                              ot.Quantity,
                                              ot.Price,
                                              Total = (ot.Price ?? 0) * (ot.Quantity ?? 0)
                                          }).ToList()
                             }).FirstOrDefault();

                if (order == null)
                    return Json(new { success = false, message = "Order not found" });

                // ✅ Calculate totals
                decimal subtotal = order.Items.Sum(i => i.Total);
                decimal discount = order.DiscountAmount ?? 0;
                //decimal finalTotal = subtotal - discount + (order.VatAmount ?? 0);

                // ✅ Return combined response
                return Json(new
                {
                    success = true,
                    data = new
                    {
                        company = new
                        {
                            company.CompanyName,
                            company.CompanyNameArabic,
                            company.Email,
                            company.Phone,
                            company.Address,
                            company.AddressArabic,
                            LogoBase64 = company.Logo
                        },
                        user = order.User,
                        order = new
                        {
                            order.OrderId,
                            order.CreatedAt,
                            order.PaymentType,
                            order.PaymentStatus,
                            order.CouponCode,
                            order.VatPercent,
                            order.VatAmount
                        },
                        items = order.Items,
                        subtotal,
                        discount,
                        //finalTotal
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }



        [Authorize(Roles = "Admin")]
        public IActionResult LatestOrdersNotification(int? lastOrderId)
        {
            var query = _context.Orders.AsQueryable();

            if (lastOrderId.HasValue && lastOrderId.Value > 0)
            {
                query = query.Where(o => o.OrderId > lastOrderId.Value);
            }

            var latestOrders = query
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .Select(o => new LatestOrderItem
                {
                    OrderId = o.OrderId,
                    PaymentType = o.PaymentType ?? "N/A",
                    CreatedAt = o.CreatedAt
                })
                .ToList();

            var newOrdersCount = latestOrders.Count;
            var lastId = latestOrders.Any() ? latestOrders.Max(o => o.OrderId) : (lastOrderId ?? 0);

            var model = new LatestOrdersViewModel
            {
                Orders = latestOrders,
                NewOrdersCount = newOrdersCount,
                LastOrderId = lastId
            };

            return PartialView("_Notificatiopartial", model);
        }
    }
}



