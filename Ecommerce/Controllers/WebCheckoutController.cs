using System.Data;
using MF.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using System.Text.Json;
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

        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly IDataProtector _quoteProtector;

        public WebCheckoutController(EcommerceDbContext context, IConfiguration configuration,
            IWebHostEnvironment environment, IDataProtectionProvider protection)
        {
            _context = context;
            _configuration = configuration;
            _environment = environment;
            _quoteProtector = protection.CreateProtector("Ecom.SellerCheckout.Quote.v1");
        }

        private bool SellerCheckoutEnabled => SellerCartAccess.CheckoutEnabled(_configuration, _environment);

        private async Task<List<UserAddress>> SellerAddresses(int customerId, CancellationToken ct) =>
            await _context.UserAddresses.AsNoTracking().Where(a => a.UserId == customerId && a.isdeleted == 0)
                .OrderByDescending(a => a.IsDefault).ThenBy(a => a.AddressId).ToListAsync(ct);

        private async Task<bool> ValidSellerAddress(int customerId, int addressId, CancellationToken ct)
        {
            var address = await _context.UserAddresses.AsNoTracking().SingleOrDefaultAsync(a =>
                a.UserId == customerId && a.AddressId == addressId && a.isdeleted == 0, ct);
            return address != null && !string.IsNullOrWhiteSpace(address.Street) && !string.IsNullOrWhiteSpace(address.Phone)
                && int.TryParse(address.Area, out var area) && int.TryParse(address.Block, out var block)
                && await _context.Block.AnyAsync(b => b.Id == block && b.AreaId == area, ct);
        }

        private async Task<(decimal Delivery, decimal Vat)?> SellerCharges(CancellationToken ct)
        {
            var company = await _context.CompanyDetails.AsNoTracking().OrderByDescending(c => c.CompanyId)
                .Select(c => new { c.DeliveryCharge, c.Vat }).FirstOrDefaultAsync(ct);
            if (company == null || company.DeliveryCharge < 0 || company.Vat < 0 || company.Vat > 100) return null;
            return (Money(company.DeliveryCharge), company.Vat);
        }

        private static SellerQuoteLine[] QuoteLines(IEnumerable<SellerCartLine> rows) =>
            rows.OrderBy(r => r.ListingId).Select(r => new SellerQuoteLine(r.ListingId, r.Quantity, r.SavedPrice)).ToArray();

        private static bool ReadyForSellerQuote(List<SellerCartLine> rows) =>
            rows.Count is > 0 and <= 100 && rows.All(r => r.CurrentPrice == r.SavedPrice &&
                r.Quantity > 0 && r.AvailableQuantity >= r.Quantity);

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Seller(CancellationToken ct)
        {
            if (!SellerCheckoutEnabled) return NotFound();
            if (await SellerCartAccess.CustomerAsync(_context, HttpContext, ct) is not int customerId) return Unauthorized();
            if (await _context.CartItems.AnyAsync(c => c.UserId == customerId, ct))
            {
                TempData["SellerCartMessage"] = "Remove or review old cart items before checkout. / راجع عناصر السلة القديمة قبل الدفع.";
                return RedirectToAction("Cart", "WebCart");
            }
            var rows = await SellerCartAccess.ReadAsync(_context, customerId, ct);
            var charges = await SellerCharges(ct);
            if (!ReadyForSellerQuote(rows) || charges == null)
            {
                TempData["SellerCartMessage"] = "Review seller quantities, prices and checkout settings. / راجع السلة وإعدادات الدفع.";
                return RedirectToAction("Cart", "WebCart");
            }
            var quote = new SellerCheckoutQuote(Guid.NewGuid(), customerId, DateTime.UtcNow.AddMinutes(10),
                charges.Value.Delivery, charges.Value.Vat, QuoteLines(rows));
            if (!quote.IsValid()) return Conflict("Invalid checkout amount.");
            return View(new SellerCheckoutPage { Lines = rows, Addresses = await SellerAddresses(customerId, ct),
                Quote = quote, Token = quote.Protect(_quoteProtector) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceSellerOrder(string token, int addressId, CancellationToken ct)
        {
            if (!SellerCheckoutEnabled) return NotFound();
            if (!ModelState.IsValid || addressId <= 0) return BadRequest();
            if (await SellerCartAccess.CustomerAsync(_context, HttpContext, ct) is not int customerId) return Unauthorized();
            var quote = SellerCheckoutQuote.Read(_quoteProtector, token, customerId);
            if (quote == null) return BadRequest("Invalid checkout quotation.");
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                if (await SellerCartAccess.CustomerAsync(_context, HttpContext, ct) != customerId) return Unauthorized();
                var existing = await _context.Orders.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(o =>
                    o.CustomerId == customerId && o.MarketplaceRequestId == quote.RequestId, ct);
                if (existing != null)
                {
                    var snapshots = await _context.OrderItems.IgnoreQueryFilters().AsNoTracking()
                        .Where(i => i.OrderId == existing.OrderId && i.VendorListingId != null)
                        .Select(i => new SellerQuoteLine(i.VendorListingId!.Value, i.Quantity!.Value, i.Price!.Value)).ToListAsync(ct);
                    if (existing.DeliveryAddress != addressId || existing.TotalAmount != quote.Total ||
                        existing.DeliveryCharge != quote.DeliveryCharge || existing.VatPercent != quote.VatPercent ||
                        !quote.MatchesLines(snapshots)) return Conflict("Request already used with different details.");
                    return RedirectToAction(nameof(SellerReceipt), new { requestId = quote.RequestId });
                }
                if (await _context.CartItems.AnyAsync(c => c.UserId == customerId, ct))
                    return Conflict("Review old cart items before checkout.");
                if (quote.ExpiresAtUtc <= DateTime.UtcNow) return Conflict("Quote expired. Review checkout again.");
                if (!await ValidSellerAddress(customerId, addressId, ct)) return BadRequest("Invalid delivery address.");
                var rows = await SellerCartAccess.ReadAsync(_context, customerId, ct);
                var charges = await SellerCharges(ct);
                if (!ReadyForSellerQuote(rows) || !quote.MatchesLines(QuoteLines(rows)) || charges == null ||
                    charges.Value.Delivery != quote.DeliveryCharge || charges.Value.Vat != quote.VatPercent)
                    return Conflict("Cart or checkout charges changed. Review checkout again.");

                var now = DateTime.Now;
                var order = new OrderModel { CustomerId = customerId, MarketplaceRequestId = quote.RequestId,
                    TotalAmount = quote.Total, VatAmount = quote.VatAmount, VatPercent = quote.VatPercent,
                    DeliveryCharge = quote.DeliveryCharge, DiscountAmount = 0, Status = 1, PaymentStatus = 2,
                    PaymentType = "Online", DeliveryAddress = addressId, OrderTransaction = "0",
                    CreatedAt = now, UpdatedAt = now, NewNotification = 0 };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(ct);
                var linesJson = JsonSerializer.Serialize(quote.Lines, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var expires = DateTime.UtcNow.AddMinutes(15);
                await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    EXEC dbo.AttachMarketplaceOrder @OrderId={order.OrderId}, @CustomerId={customerId},
                        @ExpectedLines={linesJson}, @ExpiresAtUtc={expires}
                    """, ct);
                if (_configuration.GetValue("Marketplace:FinancePreviewEnabled",false))
                    await _context.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.SnapshotVendorCommission @OrderId={order.OrderId}",ct);
                await transaction.CommitAsync(ct);
                return RedirectToAction(nameof(SellerReceipt), new { requestId = quote.RequestId });
            }
            catch (SqlException ex) when (ex.Number is 1205 or 2601 or 2627 or >= 51400 and <= 51499 or >= 51700 and <= 51799 or >= 52404 and <= 52406)
            {
                return Conflict("Order not confirmed. Check the receipt using the same checkout request before retrying.");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 1205 or 2601 or 2627 })
            {
                return Conflict("Order not confirmed. Retry the same checkout request.");
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> SellerReceipt(Guid requestId, CancellationToken ct, string? paymentId = null)
        {
            if (!SellerCheckoutEnabled) return NotFound();
            if (await SellerCartAccess.CustomerAsync(_context, HttpContext, ct) is not int customerId) return Unauthorized();
            var receipt = await CustomerSellerOrders(customerId).SingleOrDefaultAsync(o => o.RequestId == requestId, ct);
            if (receipt == null) return NotFound();
            ViewBag.SandboxPaymentEnabled = SellerSandboxPaymentEnabled;
            ViewBag.PaymentLookupId = SellerSandboxPaymentEnabled && MarketplacePaymentEvidence.IsValidLookupId(paymentId)
                ? paymentId : null;
            ViewBag.Suborders = await _context.Database.SqlQuery<CustomerSuborderRow>($"""
                SELECT v.VendorOrderId,v.VendorName,v.VendorNameArb,v.MerchandiseSubtotal,v.FulfillmentStatus
                FROM dbo.VendorOrders v JOIN dbo.Orders o ON o.OrderId=v.OrderId
                WHERE o.CustomerId={customerId} AND o.MarketplaceRequestId={requestId}
                """).OrderBy(v=>v.VendorOrderId).ToListAsync(ct);
            return View(receipt);
        }
        private IQueryable<SellerOrderReceipt> CustomerSellerOrders(int customerId) =>
            _context.Database.SqlQuery<SellerOrderReceipt>($"""
                SELECT OrderId,RequestId,Total,PaymentStatus,CreatedAt,AttemptState,ReservationState
                FROM dbo.MarketplaceCustomerOrders WHERE CustomerId={customerId}
                """);

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> SellerOrders(int page=1,CancellationToken ct=default)
        {
            if (!SellerCheckoutEnabled) return NotFound();
            if (await SellerCartAccess.CustomerAsync(_context,HttpContext,ct) is not int customerId) return Unauthorized();
            page=Math.Clamp(page,1,100000);
            var rows=await CustomerSellerOrders(customerId).OrderByDescending(o=>o.OrderId)
                .Skip((page-1)*25).Take(26).ToListAsync(ct);
            ViewBag.Page=page; ViewBag.HasNext=rows.Count>25;
            return View(rows.Take(25).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSellerUnstartedOrder(Guid requestId,CancellationToken ct)
        {
            if (!SellerCheckoutEnabled) return NotFound();
            if (await SellerCartAccess.CustomerAsync(_context,HttpContext,ct) is not int customerId) return Unauthorized();
            var order=await CustomerSellerOrders(customerId).SingleOrDefaultAsync(o=>o.RequestId==requestId,ct);
            if (order==null) return NotFound();
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.CancelUnstartedMarketplaceOrder @OrderId={order.OrderId},@CustomerId={customerId}",ct);
                TempData["SellerOrderMessage"]="Reservation released. / تم تحرير الحجز.";
            }
            catch(SqlException ex) when(ex.Number==51806 || ex.Number is >=51400 and <=51499)
            {
                TempData["SellerOrderMessage"]="Cannot release this order; payment review may be required. / يلزم مراجعة الدفع.";
            }
            return RedirectToAction(nameof(SellerReceipt),new {requestId});
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> SellerReturns(Guid requestId,CancellationToken ct)
        {
            if (!SellerCheckoutEnabled || !_configuration.GetValue("Marketplace:ReturnWorkflowEnabled",false)) return NotFound();
            if (await SellerCartAccess.CustomerAsync(_context,HttpContext,ct) is not int customerId) return Unauthorized();
            if (!await CustomerSellerOrders(customerId).AnyAsync(o=>o.RequestId==requestId,ct)) return NotFound();
            ViewBag.OrderRequestId=requestId;
            ViewBag.Requests=await _context.Database.SqlQuery<MarketplaceReturnRow>($"""
                SELECT ReturnId,OrderId,CustomerId,ProductName,ProductNameArb,Quantity,Reason,Status,RequestedAtUtc
                FROM dbo.MarketplaceReturnReview WHERE CustomerId={customerId} AND OrderRequestId={requestId}
                """).OrderByDescending(r=>r.ReturnId).ToListAsync(ct);
            var items=await _context.Database.SqlQuery<MarketplaceReturnItem>($"""
                SELECT OrderItemId,ProductName,ProductNameArb,PurchasedQuantity,RemainingQuantity
                FROM dbo.MarketplaceReturnItems WHERE CustomerId={customerId} AND OrderRequestId={requestId}
                """).OrderBy(r=>r.OrderItemId).ToListAsync(ct);
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestSellerReturn(Guid orderRequestId,int orderItemId,Guid requestId,int quantity,string reason,CancellationToken ct)
        {
            if (!SellerCheckoutEnabled || !_configuration.GetValue("Marketplace:ReturnWorkflowEnabled",false)) return NotFound();
            if (await SellerCartAccess.CustomerAsync(_context,HttpContext,ct) is not int customerId) return Unauthorized();
            if (!ModelState.IsValid || requestId==Guid.Empty || quantity<=0 || string.IsNullOrWhiteSpace(reason) || reason.Length>200) return BadRequest();
            if (!await _context.Database.SqlQuery<int>($"""
                SELECT OrderItemId AS Value FROM dbo.MarketplaceReturnItems WHERE CustomerId={customerId}
                AND OrderRequestId={orderRequestId} AND OrderItemId={orderItemId}
                """).AnyAsync(ct)) return NotFound();
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    EXEC dbo.RequestMarketplaceReturn @CustomerId={customerId},@OrderItemId={orderItemId},
                         @RequestId={requestId},@Quantity={quantity},@Reason={reason.Trim()}
                    """,ct);
                TempData["ReturnMessage"]="Return request saved. / تم حفظ طلب الإرجاع.";
            }
            catch(SqlException ex) when(ex.Number is >=52300 and <=52303)
            {
                TempData["ReturnMessage"]="Return request rejected. Review remaining quantity. / راجع الكمية المتبقية.";
            }
            return RedirectToAction(nameof(SellerReturns),new {requestId=orderRequestId});
        }

        private bool SellerSandboxPaymentEnabled => MarketplacePaymentReconciliation.SandboxEnabled(_configuration, _environment);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MarketplacePayment")]
        public async Task<IActionResult> StartSellerSandboxPayment(Guid requestId, [FromServices] IPaymentService gateway, CancellationToken ct)
        {
            if (!SellerSandboxPaymentEnabled) return NotFound();
            if (await SellerCartAccess.CustomerAsync(_context, HttpContext, ct) is not int customerId) return Unauthorized();
            var order = await _context.Orders.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(o =>
                o.CustomerId == customerId && o.MarketplaceRequestId == requestId && o.PaymentStatus == 2, ct);
            if (order == null) return NotFound();
            // Explicit test return URL, never derive it from an untrusted Host header.
            if (!Uri.TryCreate(_configuration["Marketplace:SandboxReturnUrl"], UriKind.Absolute, out var callback) ||
                callback.Scheme != "https" || callback.IsLoopback || !string.IsNullOrEmpty(callback.UserInfo) ||
                !string.IsNullOrEmpty(callback.Query) || !string.IsNullOrEmpty(callback.Fragment) ||
                !callback.AbsolutePath.EndsWith("/WebCheckout/SellerReceipt", StringComparison.OrdinalIgnoreCase))
                return Conflict("Configure the sandbox receipt return URL.");
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.BeginMarketplacePayment @OrderId={order.OrderId}, @CustomerId={customerId}", ct);
                var returnUrl = callback.AbsoluteUri + "?requestId=" + requestId.ToString("D");
                var response = await gateway.SendPayment(new SendPaymentRequest
                {
                    CustomerName = "Marketplace Sandbox", NotificationOption = "LNK",
                    InvoiceValue = order.TotalAmount, DisplayCurrencyIso = "KWD",
                    CustomerReference = requestId.ToString("N"), CallBackUrl = returnUrl, ErrorUrl = returnUrl,
                    Language = "en"
                });
                if (response?.IsSuccess != true || response.Data == null || response.Data.InvoiceId <= 0 ||
                    response.Data.CustomerReference != requestId.ToString("N") ||
                    !Uri.TryCreate(response.Data.InvoiceURL, UriKind.Absolute, out var paymentUrl) ||
                    paymentUrl.Scheme != "https" || !string.IsNullOrEmpty(paymentUrl.UserInfo) ||
                    !string.Equals(paymentUrl.Host,"demo.myfatoorah.com",StringComparison.OrdinalIgnoreCase))
                    return Conflict("Sandbox invoice requires reconciliation. Do not initiate another payment.");
                await _context.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.LinkMarketplaceInvoice @OrderId={order.OrderId}, @InvoiceId={response.Data.InvoiceId}, @MerchantReference={requestId}", ct);
                return Redirect(paymentUrl.AbsoluteUri);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // An uncertain send must not create a second external invoice.
                return Conflict("Payment attempt requires reconciliation. Do not initiate another payment.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MarketplacePayment")]
        public async Task<IActionResult> VerifySellerSandboxPayment(Guid requestId, [FromServices] IPaymentService gateway,
            CancellationToken ct, string? paymentId = null)
        {
            if (!SellerSandboxPaymentEnabled) return NotFound();
            if (await SellerCartAccess.CustomerAsync(_context, HttpContext, ct) is not int customerId) return Unauthorized();
            var order = await _context.Orders.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(o =>
                o.CustomerId == customerId && o.MarketplaceRequestId == requestId, ct);
            if (order == null) return NotFound();

            IActionResult Receipt(string message, bool retainLookup = false)
            {
                TempData["SellerOrderMessage"] = message;
                return RedirectToAction(nameof(SellerReceipt), new
                {
                    requestId,
                    paymentId = retainLookup && MarketplacePaymentEvidence.IsValidLookupId(paymentId) ? paymentId : null
                });
            }

            var states = await _context.Database.SqlQuery<string>($"""
                SELECT State AS Value FROM dbo.MarketplacePaymentAttempts
                WHERE OrderId={order.OrderId} AND MerchantReference={requestId}
                """).ToListAsync(ct);
            if (states.Count != 1 || states[0] is not ("Starting" or "Linked" or "Paid"))
                return Receipt("No active payment attempt. Contact support. / لا توجد محاولة دفع نشطة.");
            if (states[0] == "Paid" && order.PaymentStatus == 1)
                return RedirectToAction(nameof(SellerReceipt), new { requestId });

            try
            {
                if (states[0] == "Starting")
                {
                    if (!MarketplacePaymentEvidence.IsValidLookupId(paymentId))
                        return Receipt("Enter the gateway payment reference or contact support. / أدخل مرجع الدفع أو اتصل بالدعم.");
                    var response = await gateway.GetPaymentStatus(new GetPaymentStatusRequest { KeyType = "PaymentId", Key = paymentId! });
                    var recovered = MarketplacePaymentEvidence.VerifiedRecoveryInvoice(response, paymentId, requestId, order.TotalAmount);
                    if (!recovered.HasValue)
                        return Receipt("Payment not verified. Do not pay again. / لم يتم التحقق من الدفع.", true);
                    await _context.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.LinkMarketplaceInvoice @OrderId={order.OrderId}, @InvoiceId={recovered.Value}, @MerchantReference={requestId}", ct);
                    await MarketplacePaymentReconciliation.ConfirmAsync(_context, order.OrderId, requestId, recovered.Value, paymentId!, order.TotalAmount, ct);
                }
                else if (!await MarketplacePaymentReconciliation.VerifyLinkedAsync(_context, gateway, order.OrderId, requestId, order.TotalAmount, ct))
                    return Receipt("Payment not verified. Do not pay again. / لم يتم التحقق من الدفع.");

                return RedirectToAction(nameof(SellerReceipt), new { requestId });
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<WebCheckoutController>>()
                    .LogWarning(ex, "Sandbox payment verification requires review for order {OrderId}.", order.OrderId);
                return Receipt("Payment requires reconciliation. Do not pay again. / يلزم مراجعة الدفع.", true);
            }
        }

        private int? CustomerId => User.IsInRole("Customer") && int.TryParse(User.FindFirst("UserId")?.Value, out var id) && HttpContext.Session.GetInt32("UserId") == id ? id : null;

        private IQueryable<CartItem> CheckoutCart(int? userId, string deviceId) => _context.CartItems.Where(c =>
            userId.HasValue ? c.UserId == userId.Value : c.UserId == 0 && c.DeviceId == deviceId);

        public IActionResult Index(string Usertype)
        {
            if (SellerCheckoutEnabled) return RedirectToAction("Cart", "WebCart");
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
            if (SellerCheckoutEnabled) return Conflict(new { message = "Use the seller checkout in this test environment." });
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
                        PasswordHash = CustomerPassword.Hash(Guid.NewGuid().ToString("N")), CreatedAt = now, UpdatedAt = now
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
