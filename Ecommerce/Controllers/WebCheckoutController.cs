using System.Data;
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
        private int? CustomerId => User.IsInRole("Customer") && int.TryParse(User.FindFirst("UserId")?.Value, out var id) && HttpContext.Session.GetInt32("UserId") == id ? id : null;

        private IQueryable<CartItem> CheckoutCart(int? userId, string deviceId) => _context.CartItems.Where(c =>
            userId.HasValue ? c.UserId == userId.Value : c.UserId == 0 && c.DeviceId == deviceId);

        public IActionResult Index(string Usertype)
        {
            int? userId = CustomerId;
            string deviceId = DeviceIdHelper.GetOrCreateDeviceId(HttpContext);
            var cartItemCount = CheckoutCart(userId, deviceId).Count();

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
   .OrderByDescending(x => x.CompanyId)
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
            model.CartItems = (from c in CheckoutCart(userId, deviceId)
                               join p in _context.Products
                               on c.ProductId equals p.ProductId
                               where p.IsActive
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
        [ValidateAntiForgeryToken]
        public IActionResult AddAddress([FromBody] UserAddress model)
        {
            var userId = CustomerId;

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

        private async Task<(Coupon? Coupon, decimal Discount, string? Error)> QuoteCoupon(string? code, int? userId, decimal subtotal)
        {
            if (string.IsNullOrWhiteSpace(code)) return (null, 0m, null);
            if (!userId.HasValue) return (null, 0m, "Sign in to use a coupon.");
            var matches = await _context.Coupons.Where(c => c.Code == code.Trim()).Take(2).ToListAsync();
            var coupon = matches.Count == 1 ? matches[0] : null;
            var now = DateTime.Now;
            if (coupon == null || !coupon.IsActive || coupon.ValidFrom > now || coupon.ValidTo < now ||
                coupon.MinOrderAmount > subtotal || coupon.DiscountValue.GetValueOrDefault() <= 0)
                return (null, 0m, "Invalid, expired or ineligible coupon.");
            var usage = _context.CouponUsage.Where(x => x.CouponId == coupon.CouponId);
            if (await usage.AnyAsync(x => x.UserId == userId.Value))
                return (null, 0m, "You already used this coupon.");
            if (coupon.NoOfUsers > 0 && await usage.Select(x => x.UserId).Distinct().CountAsync() >= coupon.NoOfUsers.Value)
                return (null, 0m, "Coupon usage limit reached.");
            decimal value = coupon.DiscountValue.Value;
            decimal discount;
            if (string.Equals(coupon.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase) && value <= 100)
                discount = subtotal * value / 100m;
            else if (string.Equals(coupon.DiscountType, "Amount", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(coupon.DiscountType, "Fixed", StringComparison.OrdinalIgnoreCase))
                discount = value;
            else return (null, 0m, "Invalid coupon configuration.");
            if (coupon.MaxDiscountAmount > 0) discount = Math.Min(discount, coupon.MaxDiscountAmount);
            return (coupon, Money(Math.Min(discount, subtotal)), null);
        }

        private static decimal Money(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyCoupon([FromBody] ApplyCouponRequest request)
        {
            var userId = CustomerId;
            if (!userId.HasValue || !await _context.Customers.AnyAsync(c => c.UserId == userId.Value && c.IsActive && c.IsDeleted == 0))
                return Unauthorized(new { message = "Sign in to use a coupon." });
            if (request == null || string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 50)
                return BadRequest(new { message = "Enter a valid coupon code." });
            var subtotal = await (from c in CheckoutCart(userId, DeviceIdHelper.GetOrCreateDeviceId(HttpContext))
                                  join p in _context.Products on c.ProductId equals p.ProductId
                                  where p.IsActive && c.Quantity > 0
                                  select (decimal?)(p.Price * c.Quantity)).SumAsync() ?? 0m;
            var quote = await QuoteCoupon(request.Code, userId, subtotal);
            if (quote.Error != null) return BadRequest(new { message = quote.Error });
            return Ok(new { success = true, CouponId = quote.Coupon!.CouponId, CouponCode = quote.Coupon.Code,
                DiscountApplied = quote.Discount, FinalAmount = subtotal - quote.Discount, subtotal });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderDto request)
        {
            if (request?.Items == null || request.Items.Count == 0 || request.Items.Count > 200 ||
                request.Items.Any(i => i == null || i.ProductId <= 0 || i.Quantity <= 0) ||
                request.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
                return BadRequest(new { message = "Invalid order request." });

            // Existing storefront creates an unpaid order, not a gateway payment.
            if (!string.Equals(request.PaymentMethod, "Online", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Unsupported payment method." });

            var sessionUserId = CustomerId;
            var deviceId = DeviceIdHelper.GetOrCreateDeviceId(HttpContext);
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // Limit this website path to the current customer's or anonymous device's actual basket.
                var cart = await CheckoutCart(sessionUserId, deviceId).ToListAsync();
                var quantities = cart.GroupBy(c => c.ProductId).ToDictionary(g => g.Key, g => g.Sum(c => (long)c.Quantity));
                if (quantities.Count != request.Items.Count || request.Items.Any(i => !quantities.TryGetValue(i.ProductId, out var qty) || qty != i.Quantity))
                    return Conflict(new { message = "Your cart has changed. Refresh checkout and review your order." });

                decimal subtotal = 0m;
                // Keep legacy inventory semantics until all mobile and gateway writers are migrated.
                foreach (var item in request.Items.OrderBy(i => i.ProductId))
                {
                    var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == item.ProductId && p.IsActive);
                    if (product == null || product.Price < 0 || item.Price != product.Price)
                        return Conflict(new { message = "A product or price has changed. Refresh checkout and review your order." });
                    var received = await _context.ProductStocks.Where(p => p.ProductId == item.ProductId).SumAsync(p => (long?)p.StockQty) ?? 0;
                    var sold = await _context.OrderItems.Where(p => p.ProductId == item.ProductId).SumAsync(p => (long?)p.Quantity) ?? 0;
                    if (received - sold < item.Quantity)
                        return Conflict(new { message = "Insufficient stock. Refresh your cart." });
                    item.Price = product.Price;
                    subtotal += item.Price * item.Quantity;
                }

                var quote = await QuoteCoupon(request.CouponCode, sessionUserId, subtotal);
                if (quote.Error != null) return BadRequest(new { message = quote.Error });
                if (request.DiscountAmount != quote.Discount)
                    return Conflict(new { message = "Coupon discount has changed. Refresh checkout and apply the coupon again." });
                var company = await _context.CompanyDetails.OrderByDescending(c => c.CompanyId).FirstOrDefaultAsync();
                decimal delivery = company?.DeliveryCharge ?? 0m;
                decimal vatPercent = company?.Vat ?? 0m;
                if (delivery < 0 || vatPercent < 0 || vatPercent > 100)
                    return Conflict(new { message = "Checkout configuration is invalid." });
                var taxable = Money(subtotal - quote.Discount);
                var vatAmount = Money(taxable * vatPercent / 100m);
                var finalTotal = Money(taxable + vatAmount + delivery);
                if (request.TotalAmount != finalTotal)
                    return Conflict(new { message = "Order total has changed. Refresh checkout and review your order." });

                var now = DateTime.Now;
                int userId;
                if (sessionUserId.HasValue)
                {
                    userId = sessionUserId.Value;
                    if (!await _context.Customers.AnyAsync(c => c.UserId == userId && c.IsActive && c.IsDeleted == 0))
                        return Unauthorized(new { message = "Customer session is invalid." });
                    if (!await _context.UserAddresses.AnyAsync(a => a.AddressId == request.DeliveryAddress && a.UserId == userId && a.isdeleted == 0))
                        return BadRequest(new { message = "Invalid delivery address." });
                }
                else
                {
                    var email = request.Email?.Trim();
                    var phone = request.Mobile?.Trim();
                    if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 100 ||
                        string.IsNullOrWhiteSpace(email) || email.Length > 150 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) ||
                        phone == null || !System.Text.RegularExpressions.Regex.IsMatch(phone, @"\A[0-9]{8}\z") ||
                        string.IsNullOrWhiteSpace(request.Street) || !request.AreaId.HasValue || !request.BlockId.HasValue ||
                        !await _context.Block.AnyAsync(b => b.Id == request.BlockId.Value && b.AreaId == request.AreaId.Value))
                        return BadRequest(new { message = "Invalid guest or delivery details." });

                    // Never attach an anonymous checkout to an existing account using email or phone.
                    if (await _context.Customers.AnyAsync(c => (c.IsDeleted == 0 || c.IsDeleted == null) &&
                        (c.Email == email || c.Phone == phone)))
                        return Conflict(new { message = "These contact details already exist. Sign in or contact support before placing an order." });

                    var guest = new Customers
                    {
                        FullName = request.FullName.Trim(), Email = email, Phone = phone, RoleId = 2,
                        IsActive = true, IsDeleted = 0, CustType = "Guest", ShareholderNo = request.ShareholderNo,
                        PasswordHash = Guid.NewGuid().ToString("N"), CreatedAt = now, UpdatedAt = now
                    };
                    _context.Customers.Add(guest);
                    await _context.SaveChangesAsync();
                    userId = guest.UserId;
                    var address = new UserAddress
                    {
                        UserId = userId, Title = "Home", Area = request.AreaId.Value.ToString(), Block = request.BlockId.Value.ToString(),
                        Street = request.Street, HouseNo = request.HouseNo, Floor = request.Floor, Phone = phone,
                        CreatedAt = now, IsDefault = true, isdeleted = 0
                    };
                    _context.UserAddresses.Add(address);
                    await _context.SaveChangesAsync();
                    request.DeliveryAddress = address.AddressId;
                }

                var order = new OrderModel
                {
                    CustomerId = userId, TotalAmount = finalTotal, DiscountAmount = quote.Discount,
                    VatPercent = vatPercent, VatAmount = vatAmount, Status = 1, PaymentStatus = 2,
                    NewNotification = 0, PaymentType = "Online", CouponCode = quote.Coupon?.Code,
                    CreatedAt = now, UpdatedAt = now, DeliveryAddress = request.DeliveryAddress,
                    OrderTransaction = "0", DeliveryCharge = delivery
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in request.Items)
                    _context.OrderItems.Add(new OrderItemmodel { OrderId = order.OrderId, ProductId = item.ProductId, Quantity = item.Quantity, Price = item.Price });

                // Legacy AmountPaid stores the expected order amount; paid reporting filters by status.
                // Do not change its semantics until the payment reconciliation migration is complete.
                _context.Payments.Add(new Payments { OrderId = order.OrderId, AmountPaid = finalTotal,
                    PaymentMethod = "Online", PaymentStatus = 2, TransactionId = null, PaidAt = null });
                if (quote.Coupon != null)
                    _context.CouponUsage.Add(new CouponUsage { CouponId = quote.Coupon.CouponId, UserId = userId, UsedAt = now });
                _context.CartItems.RemoveRange(cart);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "Order received; payment pending.", orderId = order.OrderId,
                    subtotal, discount = quote.Discount, vatPercent, vatAmount, finalTotal });
            }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<WebCheckoutController>>().LogError(ex, "Website checkout failed.");
                return StatusCode(500, new { message = "Unable to confirm the order. Check your cart and order history before retrying." });
            }
        }

        [HttpGet]
        public IActionResult GetAddressById(int id)
        {
            int? userId = CustomerId;
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
