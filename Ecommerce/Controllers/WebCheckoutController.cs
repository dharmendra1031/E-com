using DocumentFormat.OpenXml.Spreadsheet;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{

    public class WebCheckoutController : Controller
    {
        private readonly EcommerceDbContext _context;

        public WebCheckoutController(EcommerceDbContext context)
        {
            _context = context;
        }
        public IActionResult Index(string Usertype)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string deviceId = DeviceIdHelper.GetOrCreateDeviceId(HttpContext);
            var cartItemCount = _context.CartItems.Count(c =>
    (userId.HasValue && c.UserId == userId.Value) ||
    (!userId.HasValue && !string.IsNullOrEmpty(deviceId) && c.DeviceId == deviceId)
);

            // ❌ cart empty
            if (cartItemCount == 0)
            {
                TempData["CartMessage"] = "Please purchase at least one item";
                return RedirectToAction("Index", "WebCategory");
            }
            if (!userId.HasValue && Usertype == null)
            {
                return RedirectToAction(
                    "Login",
                    "UserAccount",
                    new { returnUrl = "/WebCheckout/Index" }
                );
            }

            var model = new CheckoutviewModel
            {
                AddressList = new List<UserAddress>(),
                CartItems = new List<OrderSummaryItemViewModel>()
            };
            var company = _context.CompanyDetails
   .Select(x => new
   {
       x.Vat,
       x.DeliveryCharge
   })
   .FirstOrDefault();

            if (company != null)
            {
                model.VatPercentage = company.Vat;
                model.DeliveryCharge = company.DeliveryCharge;
            }
            if (userId.HasValue)
            {
                // 🔹 Customer Info
                var customer = _context.Customers
                    .Where(x => x.UserId == userId.Value)
                    .Select(x => new
                    {
                        x.UserId,
                        x.FullName,
                        x.Email,
                        x.Phone
                    })
                    .FirstOrDefault();

                if (customer != null)
                {
                    model.UserId = customer.UserId;
                    model.FullName = customer.FullName;
                    model.Email = customer.Email;
                    model.Phone = customer.Phone;
                }

              
                // 🔹 Address List
                var addressList = _context.UserAddresses
                    .Where(x => x.UserId == userId.Value && x.isdeleted == 0)
                    .OrderByDescending(x => x.IsDefault)   
                    .ThenBy(x => x.AddressId)               
                    .ToList();

                model.AddressList = addressList;

                // ✅ Selected Address (View ko bhejne ke liye)
                model.SelectedAddressId = addressList.Any()
       ? addressList.OrderByDescending(x => x.AddressId).First().AddressId
       : 0;


                // 🔹 Cart Items
                //model.CartItems = _context.CartItems
                //    .Where(c => c.UserId == userId.Value)
                //    .Select(c => new OrderSummaryItemViewModel

            }
            model.CartItems = (from c in _context.CartItems
                               join p in _context.Products
                               on c.ProductId equals p.ProductId
                               where
    (userId.HasValue && c.UserId == userId.Value)
    || (!userId.HasValue && !string.IsNullOrEmpty(deviceId) && c.DeviceId == deviceId)
                               select new OrderSummaryItemViewModel
                               {
                                   ProductId = c.ProductId,
                                   ProductName = c.ProductName,
                                   ImageUrl = c.ImageBase64,
                                   Price = p.Price,
                                   Quantity = c.Quantity,
                               })
                    .ToList();





            model.CartTotal = model.CartItems.Sum(x => x.Price * x.Quantity);

            return View(model);
        }
        [HttpGet]
        public IActionResult GetAreaBlockData()
        {
            var areas = _context.Area
                .Select(a => new
                {
                    a.Id,
                    a.AreaName,
                    a.AreaNameArb
                })
                .ToList();

            var blocks = _context.Block
                .Select(b => new
                {
                    b.Id,
                    b.BlockName,
                    b.BlockNameArb,
                    b.AreaId
                })
                .ToList();

            return Json(new
            {
                areas,
                blocks
            });
        }

        [HttpPost]
        public IActionResult AddAddress([FromBody] UserAddress model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return Json(new { success = false, message = "User not logged in." });
            if (model == null || !int.TryParse(model.Area, out int areaId) || !int.TryParse(model.Block, out int blockId) ||
                !_context.Block.Any(b => b.Id == blockId && b.AreaId == areaId) ||
                string.IsNullOrWhiteSpace(model.Street) || string.IsNullOrWhiteSpace(model.Phone))
                return BadRequest(new { success = false, message = "Invalid address details." });

            model.UserId = userId.Value;
            model.CreatedAt = DateTime.Now;

            model.Area ??= "";
            model.Block ??= "";
            model.Street ??= "";
            model.Floor ??= "";
            model.Landmark ??= "";
            model.Governorate ??= "";
            model.isdeleted = 0;
            model.Phone =  model.Phone;

            _context.UserAddresses.Add(model);
            _context.SaveChanges();

            return Json(new { success = true, addressId = model.AddressId, title = model.Title });
        }

        [HttpPost]
        public async Task<IActionResult> ApplyCoupon([FromBody] ApplyCouponRequest request)
        {
            try
            {
                // 🔐 UserId session se
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("User not logged in");

                var coupon = await _context.Coupons
                    .Where(c => c.Code == request.Code)
                    .FirstOrDefaultAsync();

                if (coupon == null || !coupon.IsActive)
                    return BadRequest("Invalid or inactive coupon");

                var now = DateTime.Now;

                if (coupon.ValidFrom.HasValue && coupon.ValidFrom.Value > now)
                    return BadRequest("Coupon not yet valid");

                if (coupon.ValidTo.HasValue && coupon.ValidTo.Value < now)
                    return BadRequest("Coupon expired");

                if (coupon.MinOrderAmount.HasValue &&
                    request.OrderAmount < coupon.MinOrderAmount.Value)
                    return BadRequest($"Minimum order amount is {coupon.MinOrderAmount}");

                // ✅ YAHAN SESSION USERID USE HOGA
                var used = await _context.CouponUsage
                    .AnyAsync(x => x.UserId.ToString() == userId && x.CouponId == coupon.CouponId);

                if (used)
                    return BadRequest("You already used this coupon!");

                //decimal discount = 0m;
                //decimal discountValue = coupon.DiscountValue ?? 0m;
                //decimal? maxDiscount = coupon.MaxDiscountAmount;

                //if (coupon.DiscountType == "Percentage")
                //{
                //    discount = request.OrderAmount * (discountValue / 100m);
                //    if (maxDiscount.HasValue && discount > maxDiscount.Value)
                //        discount = maxDiscount.Value;
                //}
                //else if (coupon.DiscountType?.ToLower() == "Amount")
                //{
                //    discount = discountValue;
                //}
                decimal discount = 0m;
                decimal discountValue = coupon.DiscountValue ?? 0m;

                if (coupon.DiscountType == "Percentage")
                {
                    discount = request.OrderAmount * (discountValue / 100m);
                }
                else if (coupon.DiscountType == "Amount")
                {
                    discount = discountValue;
                }

                // 🔹 Final amount (negative na ho)
                decimal finalAmount = request.OrderAmount - discount;
                if (finalAmount < 0)
                {
                    finalAmount = 0;
                }



                return Ok(new
                {
                    success = true,
                    CouponId = coupon.CouponId,
                    CouponCode = coupon.Code,
                    DiscountApplied = discount,
                    FinalAmount = finalAmount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Error: {ex.Message}");
            }
        }


        [HttpPost]
        public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderDto request)
        {
            var currentCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            if (request?.Items == null || !request.Items.Any() || request.Items.Any(i => i == null || i.ProductId <= 0 || i.Quantity <= 0) ||
                request.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
                return BadRequest(new { message = currentCulture == "ar" ? "امر طلب غير صالح" : "Invalid order request" });

            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            int userId;

            // 🔹 CASE 1: Logged-in user
            if (sessionUserId != null)
            {
                userId = sessionUserId.Value;
            }
            else
            {
                // 🔹 CASE 2: GUEST USER → CREATE USER + ADDRESS

                // 1️⃣ CHECK: Email ya Mobile already exists?
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        (!string.IsNullOrEmpty(request.Email) && u.Email == request.Email)
                        || (!string.IsNullOrEmpty(request.Mobile) && u.Phone == request.Mobile)
                    );

                if (existingUser != null)
                {
                    userId = existingUser.UserId;
                }
                else
                {
                    // 2️⃣ CREATE USER
                    var newUser = new Customers
                    {
                        FullName = request.FullName,
                        Email = request.Email,
                        Phone = request.Mobile,
                        RoleId = 2,              // Customer role
                        IsActive = true,
                        CustType = "Guest",
                        ShareholderNo = request.ShareholderNo,
                        PasswordHash = "123",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.Customers.Add(newUser);
                    await _context.SaveChangesAsync();

                    userId = newUser.UserId;
                }

                // 3️⃣ CREATE ADDRESS
                var address = new UserAddress
                {
                    UserId = userId,
                    Title = "Home",
                    Area = request.AreaId.ToString(),
                    Block = request.BlockId.ToString(),
                    Street = request.Street,
                    HouseNo = request.HouseNo,
                    Floor = request.Floor,
                    Phone = request.Mobile,
                    CreatedAt = DateTime.Now,
                    IsDefault = true
                };

                _context.UserAddresses.Add(address);
                await _context.SaveChangesAsync();

                // 4️⃣ SESSION ME USER ID SET (important)
                HttpContext.Session.SetInt32("UserId", userId);

                // 5️⃣ DELIVERY ADDRESS ID SET
                request.DeliveryAddress = address.AddressId;
            }

            if (userId == null)
                return Unauthorized(new { message = "User not logged in" });
            if (!await _context.UserAddresses.AnyAsync(a => a.AddressId == request.DeliveryAddress && a.UserId == userId && a.isdeleted == 0))
                return BadRequest(new { message = currentCulture == "ar" ? "عنوان غير صالح" : "Invalid delivery address" });


            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Ensure valid datetime for SQL Server
                DateTime nowSqlSafe = DateTime.Now < new DateTime(1753, 1, 1)
                    ? new DateTime(1753, 1, 1)
                    : DateTime.Now;

                //  Get VAT % from company details
                var company = await _context.CompanyDetails
                    .OrderByDescending(c => c.CompanyId)
                    .FirstOrDefaultAsync();

                decimal DeliveryCharge = company?.DeliveryCharge ?? 0;
                decimal vatPercent = company?.Vat ?? 0;
                decimal subtotal = request.Items.Sum(i => i.Price * i.Quantity);
                decimal discountAmount = request.DiscountAmount;
                decimal afterDiscount = Math.Max(subtotal - discountAmount, 0);
                decimal vatAmount = (afterDiscount * vatPercent) / 100;
                decimal finalTotal = afterDiscount + vatAmount + DeliveryCharge;

                // Create Order
                var order = new OrderModel
                {
                    CustomerId = userId,
                    TotalAmount = finalTotal,
                    DiscountAmount = discountAmount,
                    VatPercent = vatPercent,
                    VatAmount = vatAmount,
                    Status = 1,
                    PaymentStatus = 2,
                    NewNotification = 0,
                    PaymentType = "Online",
                    CouponCode = request.CouponCode,
                    CreatedAt = nowSqlSafe,
                    UpdatedAt = nowSqlSafe,
                    DeliveryAddress = request.DeliveryAddress,
                    OrderTransaction = "0",
                    DeliveryCharge = DeliveryCharge
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();


                foreach (var item in request.Items)
                {

                    var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == item.ProductId);
                    if (product == null)
                        throw new Exception($"Product not found: {item.ProductId}");

                    /*var stock = await _context.ProductStocks
                    .FirstOrDefaultAsync(s => s.ProductId == item.ProductId);*/

                    var totalStock = await _context.ProductStocks
                            .Where(s => s.ProductId == item.ProductId)
                            .SumAsync(s => (int?)s.StockQty) ?? 0;
                    var soldQty = await _context.OrderItems
                          .Where(o => o.ProductId == item.ProductId)
                          .SumAsync(o => (int?)o.Quantity) ?? 0;

                    var stock = totalStock - soldQty;

                    if (stock == 0 || stock < item.Quantity)
                    {
                        return BadRequest(new
                        {
                            message = currentCulture == "ar"
                                ? $"الكمية غير متوفرة للمنتج: {product.NameArb}"
                                : $"Insufficient stock for product !!: {product.Name}"
                        });
                    }


                    // Add order item
                    _context.OrderItems.Add(new OrderItemmodel
                    {
                        OrderId = order.OrderId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Price = item.Price
                    });

                    // Deduct stock
                    /*                    stock.StockQty -= item.Quantity;
                    */
                    _context.Products.Update(product);
                }

                //  Add Payment record (COD = unpaid)
                _context.Payments.Add(new Payments
                {
                    OrderId = order.OrderId,
                    AmountPaid = finalTotal,
                    PaymentMethod = request.PaymentMethod,
                    PaymentStatus = 2, // Unpaid
                    TransactionId = null,
                    PaidAt = null
                });

                //  Add Coupon Usage if applicable
                if (request.CouponID > 0)
                {
                    _context.CouponUsage.Add(new CouponUsage
                    {
                        CouponId = request.CouponID,
                        UserId = order.CustomerId,
                        UsedAt = nowSqlSafe
                    });
                }

                // Clear Cart (optional)
                var cartItems = await _context.CartItems
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                if (cartItems.Any())
                    _context.CartItems.RemoveRange(cartItems);

                //  Save All Changes
                await _context.SaveChangesAsync();

                //  Commit Transaction
                await transaction.CommitAsync();

                int newNotificationCount = await _context.Orders
                .CountAsync(o => o.NewNotification == 0);

                //  Return Response
                return Ok(new
                {
                    message = "Order placed successfully!",
                    orderId = order.OrderId,
                    subtotal = subtotal,
                    discount = discountAmount,
                    vatPercent = vatPercent,
                    vatAmount = vatAmount,
                    finalTotal = finalTotal,
                    notificationCount = newNotificationCount
                });
            }
            catch (Exception ex)
            {

                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    message = "Failed to place order.",
                    error = ex.Message
                });
            }
        }


        [HttpGet]
        public IActionResult GetAddressById(int id)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Json(null);

            var address = (from ua in _context.UserAddresses
                           join ar in _context.Area
                               on ua.Area equals ar.Id.ToString()
                           join bl in _context.Block
                               on ua.Block equals bl.Id.ToString()
                           where ua.AddressId == id && ua.UserId == userId.Value && ua.isdeleted == 0
                           select new
                           {
                               AreaId = ar.Id,
                               AreaName = ar.AreaName,
                               AreaNameArb = ar.AreaNameArb,

                               BlockId = bl.Id,
                               BlockName = bl.BlockName,
                               BlockNameArb = bl.BlockNameArb,

                               ua.Street,
                               ua.HouseNo
                           }).FirstOrDefault();

            return Json(address);
        }


    }
}
