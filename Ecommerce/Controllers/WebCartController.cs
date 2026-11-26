using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;



namespace EcommerceWebApplication.Controllers
{
  
    public class WebCartController : Controller
    {
        private readonly EcommerceDbContext _context;

        private readonly IConfiguration _configuration;
        public WebCartController(EcommerceDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        private bool SellerCartEnabled => MarketplaceOffers.Enabled(_configuration) &&
            _configuration.GetValue("Marketplace:SellerCartEnabled", false);

        private async Task<int?> SellerCustomerAsync(CancellationToken ct)
        {
            if (!User.IsInRole("Customer") || !int.TryParse(User.FindFirst("UserId")?.Value, out var id) ||
                id <= 0 || HttpContext.Session.GetInt32("UserId") != id) return null;
            return await _context.Customers.AnyAsync(c => c.UserId == id && c.IsActive && c.IsDeleted == 0 &&
                c.Role != null && c.Role.IsActive && c.Role.RoleName == "Customer", ct) ? id : null;
        }

        // Only authenticated marketplace cart paths bypass the legacy global filter.
        private IQueryable<CartItem> SellerLines(int userId) => _context.CartItems.IgnoreQueryFilters()
            .Where(c => c.UserId == userId && c.VendorListingId != null);

        public async Task<IActionResult> Cart(CancellationToken ct)
        {
            if (SellerCartEnabled && await SellerCustomerAsync(ct) is int customerId)
            {
                var rows = await (from c in SellerLines(customerId).AsNoTracking()
                    join l in _context.VendorListings.AsNoTracking() on c.VendorListingId equals (int?)l.VendorListingId
                    select new EcommerceWebApplication.Models.ViewModels.SellerCartLine
                    {
                        CartItemId = c.CartItemId, ListingId = l.VendorListingId, VendorId = l.VendorId,
                        VendorName = l.Vendor.Name, VendorNameArb = l.Vendor.NameArb,
                        ProductId = c.ProductId, ProductName = l.Product.Name, ProductNameArb = l.Product.NameArb,
                        Quantity = c.Quantity, SavedPrice = c.Price
                    }).OrderBy(c => c.VendorId).ThenBy(c => c.CartItemId).ToListAsync(ct);
                var listingIds = rows.Select(c => c.ListingId).ToArray();
                if (listingIds.Length > 0)
                {
                    var offers = await MarketplaceOffers.Available(_context).Where(o => listingIds.Contains(o.ListingId))
                        .ToDictionaryAsync(o => o.ListingId, ct);
                    foreach (var row in rows)
                        if (offers.TryGetValue(row.ListingId, out var offer))
                        {
                            row.CurrentPrice = offer.Price;
                            row.AvailableQuantity = offer.AvailableQuantity;
                        }
                }
                ViewBag.SellerCart = rows;
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetSellerQuantity(int listingId, int quantity, CancellationToken ct)
        {
            if (!SellerCartEnabled) return NotFound();
            if (await SellerCustomerAsync(ct) is not int customerId) return Unauthorized();
            if (!ModelState.IsValid || listingId <= 0 || quantity < 1 || quantity > 1000000)
                return BadRequest("Invalid listing or quantity.");
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var offer = await MarketplaceOffers.Available(_context).SingleOrDefaultAsync(o => o.ListingId == listingId, ct);
            if (offer == null || offer.AvailableQuantity < quantity)
            {
                TempData["SellerCartMessage"] = "Seller offer or requested stock unavailable. / العرض أو الكمية غير متاحة.";
                return RedirectToAction(nameof(Cart));
            }
            var line = await SellerLines(customerId).SingleOrDefaultAsync(c => c.VendorListingId == listingId, ct);
            if (line == null)
            {
                if (await SellerLines(customerId).CountAsync(ct) >= 100)
                    return Conflict("Seller cart is limited to 100 listings.");
                var product = await _context.Products.AsNoTracking().Where(p => p.ProductId == offer.ProductId)
                    .Select(p => new { p.Name, p.NameArb }).SingleAsync(ct);
                line = new CartItem { UserId = customerId, VendorListingId = listingId, ProductId = offer.ProductId,
                    ProductName = product.Name, ProductNameArb = product.NameArb, DeviceId = "",
                    ImageBase64 = "", CreatedAt = DateTime.Now };
                _context.CartItems.Add(line);
            }
            line.Quantity = quantity;
            line.Price = offer.Price;
            try
            {
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
            {
                return Conflict("Cart changed. Reload and try again.");
            }
            return RedirectToAction(nameof(Cart));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSellerLine(int listingId, CancellationToken ct)
        {
            if (!SellerCartEnabled) return NotFound();
            if (await SellerCustomerAsync(ct) is not int customerId) return Unauthorized();
            await SellerLines(customerId).Where(c => c.VendorListingId == listingId).ExecuteDeleteAsync(ct);
            return RedirectToAction(nameof(Cart));
        }

        //[HttpPost]
        //public IActionResult AddOrUpdateCart(int productId, int qty = 1)
        //{
        //    try
        //    {
        //        int userId = 0;

        //        // ✅ SAFEST way to check session availability
        //        var sessionFeature = HttpContext.Features.Get<ISessionFeature>();

        //        if (sessionFeature != null)
        //        {
        //            userId = HttpContext.Session.GetInt32("UserId") ?? 0;
        //        }
        //        string deviceId = DeviceIdHelper.GetOrCreateDeviceId(HttpContext);

        //        // product details (price, name, image)
        //        var product = _context.Products.FirstOrDefault(p => p.ProductId == productId);
        //        if (product == null)
        //            return BadRequest("Product not found");

        //        CartItem existingItem;

        //        // 🔹 CASE 1: Logged-in user
        //        if (userId > 0)
        //        {
        //            existingItem = _context.CartItems
        //                .FirstOrDefault(c => c.UserId == userId && c.ProductId == productId);
        //        }
        //        else
        //        {
        //            // 🔹 CASE 2: Guest user (DeviceId)
        //            existingItem = _context.CartItems
        //                .FirstOrDefault(c => c.DeviceId == deviceId && c.UserId == 0 && c.ProductId == productId);
        //        }

        //        if (existingItem != null)
        //        {
        //            // ✅ Quantity increase
        //            existingItem.Quantity += qty;
        //        }
        //        else
        //        {
        //            // ✅ New cart item
        //            _context.CartItems.Add(new CartItem
        //            {
        //                UserId = userId,              // 0 for guest
        //                DeviceId = deviceId,
        //                ProductId = product.ProductId,
        //                ProductName = product.Name,
        //                ProductNameArb = product.NameArb,
        //                Price = product.Price,
        //                Quantity = qty,
        //                ImageBase64 = ImageBase64,
        //                CreatedAt = DateTime.Now
        //            });
        //        }

        //        _context.SaveChanges();

        //        return Ok(new { success = true, message = "Added to cart" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, ex.Message);
        //    }
        //}

        [HttpPost]
        public IActionResult AddOrUpdateCart(int productId, int qty = 1)
        {
            try
            {
                int userId = 0;

                var sessionFeature = HttpContext.Features.Get<ISessionFeature>();
                if (sessionFeature != null)
                {
                    userId = HttpContext.Session.GetInt32("UserId") ?? 0;
                }

                string deviceId = DeviceIdHelper.GetOrCreateDeviceId(HttpContext);

                var product = _context.Products.FirstOrDefault(p => p.ProductId == productId);

                if (product == null)
                    return BadRequest("Product not found");

                // check stocks

                var stockRows = _context.ProductStocks
                        .Where(s => s.ProductId == productId)
                        .OrderBy(s => s.CreatedAt)
                        .ToList();

                int totalStock = stockRows.Sum(s => Convert.ToInt32(s.StockQty));

                var orderedQty = _context.OrderItems.Where(o => o.ProductId == productId).Sum(o => (int?)o.Quantity) ?? 0;

                int availableStock = totalStock - orderedQty;


                int existingQtyInCart = 0;

                if (userId > 0)
                {
                    existingQtyInCart = _context.CartItems
                        .Where(c => c.UserId == userId && c.ProductId == productId)
                        .Select(c => c.Quantity)
                        .FirstOrDefault();
                }
                else
                {
                    existingQtyInCart = _context.CartItems
                        .Where(c => c.DeviceId == deviceId && c.UserId == 0 && c.ProductId == productId)
                        .Select(c => c.Quantity)
                        .FirstOrDefault();
                }

                int requestedQty = existingQtyInCart + qty;



                if (requestedQty > availableStock)
                {
                    return Json(new
                    {
                        success = false,
                        notStock = true,
                        message = $"Only {availableStock} item(s) available in stock"
                    });
                }

                var productImage = _context.ProductImages
                    .Where(i => i.ProductId == product.ProductId)
                    .OrderByDescending(i => i.IsPrimary)
                    .FirstOrDefault();

                string imageBase64 = null;
                if (productImage != null)
                {
                    imageBase64 = $"data:{productImage.ContentType};base64,{Convert.ToBase64String(productImage.ImageData)}";
                }

                CartItem existingItem;

                if (userId > 0)
                {
                    existingItem = _context.CartItems
                        .FirstOrDefault(c => c.UserId == userId && c.ProductId == productId);
                }
                else
                {
                    existingItem = _context.CartItems
                        .FirstOrDefault(c => c.DeviceId == deviceId && c.UserId == 0 && c.ProductId == productId);
                }

                if (existingItem != null)
                {
                    // 🔒 quantity 1 se niche na jaaye
                    if (existingItem.Quantity + qty < 1)
                    {
                        existingItem.Quantity = 1;
                    }
                    else
                    {
                        existingItem.Quantity += qty;
                    }

                    // 🖼 image safety
                    if (existingItem.ImageBase64 == null)
                        existingItem.ImageBase64 = imageBase64;
                }
                else
                {
                    _context.CartItems.Add(new CartItem
                    {
                        UserId = userId,
                        DeviceId = deviceId,
                        ProductId = product.ProductId,
                        ProductName = product.Name,
                        ProductNameArb = product.NameArb,
                        Price = product.Price,
                        Quantity = qty < 1 ? 1 : qty, // extra safety
                        ImageBase64 = imageBase64,
                        CreatedAt = DateTime.Now
                    });
                }

                _context.SaveChanges();

                return Ok(new { success = true, message = "Added to cart" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult GetCartCount()
        {
            int userId = HttpContext.Features.Get<ISessionFeature>() != null
                ? HttpContext.Session.GetInt32("UserId") ?? 0 : 0;
            string deviceId = DeviceIdHelper.GetOrCreateDeviceId(HttpContext);
            int count = _context.CartItems
                .Where(c => (userId > 0 && c.UserId == userId) ||
                            (userId == 0 && c.DeviceId == deviceId && c.UserId == 0))
                .Sum(c => (int?)c.Quantity) ?? 0;
            return Ok(new { count });
        }

        [HttpGet]
        public IActionResult GetCart()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            int userId = 0;

            // ✅ SAFEST way to check session availability
            var sessionFeature = HttpContext.Features.Get<ISessionFeature>();

            if (sessionFeature != null)
            {
                userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            }

            string deviceId = DeviceIdHelper.GetOrCreateDeviceId(HttpContext);

            var cart = _context.CartItems
                .Where(c => (userId > 0 && c.UserId == userId) ||
                            (userId == 0 && c.DeviceId == deviceId && c.UserId == 0))
                .Select(c => new CartItemDto
                {
                    ProductId = c.ProductId,
                    //ProductName = c.ProductName,
                    //ProductNameArb = c.ProductNameArb,
                    ProductName = isArabic ? c.ProductNameArb : c.ProductName,
                    Price = c.Price,
                    Quantity = c.Quantity,
                    ImageBase64 = c.ImageBase64
                })
                .ToList();

            return Ok(cart);
        }
        [HttpPost]
        public IActionResult RemoveFromCart(int productId)
        {
            int userId = 0;

            // ✅ SAFEST way to check session availability
            var sessionFeature = HttpContext.Features.Get<ISessionFeature>();

            if (sessionFeature != null)
            {
                userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            }
            string deviceId = DeviceIdHelper.GetOrCreateDeviceId(HttpContext);

            var item = _context.CartItems.FirstOrDefault(c =>
                c.ProductId == productId &&
                ((userId > 0 && c.UserId == userId) ||
                 (userId == 0 && c.DeviceId == deviceId && c.UserId == 0)));

            if (item != null)
            {
                _context.CartItems.Remove(item);
                _context.SaveChanges();
            }

            return Ok();
        }
       


    }
}
