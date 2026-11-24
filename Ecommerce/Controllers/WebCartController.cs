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

        public WebCartController(EcommerceDbContext context)
        {
            _context = context;
        }
        public IActionResult Cart()
        {
            return View();
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
