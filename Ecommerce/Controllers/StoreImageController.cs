using EcommerceWebApplication.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers
{
    // Public storefront media only. Never expose customer or administrative uploads.
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public class StoreImageController : Controller
    {
        private readonly EcommerceDbContext _context;
        public StoreImageController(EcommerceDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Product(int id) => Image(await _context.ProductImages.AsNoTracking()
            .Where(i => i.ImageId == id && i.Product.IsActive).Select(i => i.ImageData).FirstOrDefaultAsync());

        [HttpGet]
        public async Task<IActionResult> Category(int id) => Image(await _context.Categories.AsNoTracking()
            .Where(c => c.CategoryId == id && c.IsActive).Select(c => c.CategoryImage).FirstOrDefaultAsync());

        [HttpGet]
        public async Task<IActionResult> SubCategory(int id) => Image(await _context.SubCategories.AsNoTracking()
            .Where(c => c.SubCategoryId == id && c.IsActive && c.ParentCategory.IsActive).Select(c => c.SubCategoryImage).FirstOrDefaultAsync());

        [HttpGet]
        public async Task<IActionResult> Brand(int id) => Image(await _context.Brand.AsNoTracking()
            .Where(b => b.BrandId == id).Select(b => b.BrandLogo).FirstOrDefaultAsync());

        [HttpGet]
        public async Task<IActionResult> Banner(int id) => Image(await _context.Banner.AsNoTracking()
            .Where(b => b.BannerId == id).Select(b => b.BannerImage).FirstOrDefaultAsync());

        private IActionResult Image(byte[]? data)
        {
            if (data == null || data.Length < 12) return NotFound();
            // Do not serve SVG, HTML or arbitrary uploaded bytes as executable content.
            string? type = null;
            if (data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff) type = "image/jpeg";
            else if (data.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) type = "image/png";
            else if (data.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || data.AsSpan(0, 6).SequenceEqual("GIF89a"u8)) type = "image/gif";
            else if (data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WEBP"u8)) type = "image/webp";
            if (type == null) return NotFound();
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(data, type);
        }
    }
}
