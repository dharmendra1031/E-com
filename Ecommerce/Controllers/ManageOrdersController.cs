using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
    [Authorize]
    public class ManageOrdersController : Controller
    {
        private readonly EcommerceDbContext _context;

        public ManageOrdersController(EcommerceDbContext context)
        {
            _context = context;
        }

        public IActionResult PendingOrder(int? status, string? startDate, string? paymenttype, string? endDate, int? paymentstatus, int page = 1, int pageSize = 10)
        {
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
            DateTime parsedStartDate, parsedEndDate;
            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate) &&
                DateTime.TryParseExact(startDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedStartDate) &&
                DateTime.TryParseExact(endDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedEndDate))
            {
                query = query.Where(x => x.Orderdate.Date >= parsedStartDate.Date && x.Orderdate.Date <= parsedEndDate.Date);
            }

            // Sort descending by Order Date
            query = query.OrderByDescending(x => x.Orderdate);

            var data = query.ToList();


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

            int totalItems = data.Count;
            var pagedData = data
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

            return View(pagedModel);
        }


        [HttpGet]
        public IActionResult GetStatusList(int orderId)
        {
            var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId);
            var currentStatusId = order?.Status ?? 0;

            var statusList = _context.OrderStatus
                .Select(s => new SelectListItem
                {
                    Value = s.OrderstatusId.ToString(),
                    Text = s.OrderstatusName,
                    Selected = (s.OrderstatusId == currentStatusId)
                }).ToList();

            return Json(statusList);
        }
        public IActionResult GetOrderDetails(int id)
        {
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

        [HttpPost]
        public JsonResult VerifyPassword([FromBody] PasswordRequest model)
        {
            if (string.IsNullOrEmpty(model.Password))
            {
                return Json(new { valid = false });
            }

            var user = _context.Users.FirstOrDefault(u => u.RoleId == 1 && u.PasswordHash == model.Password);
            return Json(new { valid = user != null });
        }

        [HttpPost]
        public IActionResult UpdateOrderStatus([FromBody] ChangeStatusRequest model)
        {
            var order = _context.Orders.FirstOrDefault(o => o.OrderId == model.OrderId);
            if (order == null)
                return Json(new { success = false, message = "Order not found" });

            var previousStatus = order.Status;

            order.Status = model.CurrentStatus;
            _context.SaveChanges();

            var statusChange = new ChangeOrderStatus
            {
                OrderId = model.OrderId,
                PreviousStatus = previousStatus,
                CurrentStatus = model.CurrentStatus,
                Remark = string.IsNullOrWhiteSpace(model.Remark) ? null : model.Remark,
                CreatedAt = DateTime.Now,
                CreatedBy = 1
            };

            _context.ChangeOrderStatus.Add(statusChange);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult PaymentstatusList(int orderId)
        {
            var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId);
            var paymentstatusId = order?.PaymentStatus ?? 0;

            var statusList = _context.PaymentStatus
                .Select(s => new SelectListItem
                {
                    Value = s.PaymentstatusId.ToString(),
                    Text = s.PaymentstatusName,
                    Selected = (s.PaymentstatusId == paymentstatusId)
                }).ToList();

            return Json(statusList);
        }
        [HttpPost]
        public JsonResult UpdatePaymentStatus(int orderId, int PaymentStatus)
        {
            try
            {
                bool isUpdated = false;

                var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId);
                if (order != null)
                {
                    order.PaymentStatus = PaymentStatus;
                    isUpdated = true;
                }

                var payment = _context.Payments.FirstOrDefault(p => p.OrderId == orderId);
                if (payment != null)
                {
                    payment.PaymentStatus = PaymentStatus;
                    payment.PaidAt = DateTime.Now;
                    _context.SaveChanges();
                    isUpdated = true;
                }


                if (isUpdated)
                {
                    _context.SaveChanges();
                    return Json(new
                    {
                        success = true,
                        message = "Payment status updated successfully."
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = "No matching Order or Payment found for the given OrderId."
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Error: " + ex.Message
                });
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
        }


        [HttpGet]
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



