using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using System.Drawing;

using System.Drawing.Imaging;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
   [Authorize]
    public class ProductsController : Controller
    {

        private readonly EcommerceDbContext _context;

        public ProductsController(EcommerceDbContext context)
        {
            _context = context;
        }
       
        public IActionResult ProductList(string? name, string? category, decimal? price, int? status, int page = 1, int pageSize = 10)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;

            var productsQuery = _context.Products.AsNoTracking().AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(name))
                productsQuery = productsQuery.Where(p => p.Name.Contains(name) || p.NameArb.Contains(name));


            if (!string.IsNullOrEmpty(category))
                productsQuery = productsQuery.Where(p => p.Category.CategoryId.ToString() == category);

            if (price.HasValue)
            {
                string targetPrefix = ((int)price.Value).ToString();
                productsQuery = productsQuery.Where(p =>
                    ((int)p.Price).ToString().StartsWith(targetPrefix));
            }

            //if (status.HasValue)
            //{
            //    if (status == 1)
            //        productsQuery = productsQuery.Where(p => p.Stock > 0);
            //    else if (status == 0)
            //        productsQuery = productsQuery.Where(p => p.Stock <= 0);
            //}
            if (HttpContext.Request.Query.ContainsKey("status"))
            {
                // 👇 Status explicitly selected by user
                if (status == 1)
                {
                    productsQuery = productsQuery.Where(p => p.IsActive);
                }
                else if (status == 0)
                {
                    productsQuery = productsQuery.Where(p => !p.IsActive);
                }
            }
            else
            {
               
                productsQuery = productsQuery.Where(p => p.IsActive);
            }



            pageSize = Math.Clamp(pageSize, 1, 100);
            int totalItems = productsQuery.Count();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize)));
            var pagedData = productsQuery.OrderByDescending(p => p.ProductId)
                .Skip((page - 1) * pageSize).Take(pageSize).Select(p => new ProductListViewModel
            {
                ProductId = p.ProductId,
              
                Name = isArabic ? p.NameArb : p.Name,
                Price = p.Price,
                Stock =
          (_context.ProductStocks.Where(s => s.ProductId == p.ProductId)
              .Sum(s => (int?)s.StockQty) ?? 0)
          - (_context.OrderItems.Where(o => o.ProductId == p.ProductId)
              .Sum(o => (int?)o.Quantity) ?? 0),
                CategoryName = isArabic ? p.Category.CNameArb : p.Category.CName,
                SubCategoryName = isArabic ? p.SubCategory.SCNameArb : p.SubCategory.SCName,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                ProductImageBase64 = p.ProductImages
                    .OrderBy(img => img.ImageId)
                    .Select(img => "data:" + img.ContentType + ";base64," +
                                   Convert.ToBase64String(img.ImageData))
                    .FirstOrDefault()
            }).ToList();



            var pagedModel = new PagedResult<ProductListViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };


            // Dropdown for categories
            ViewBag.CategoryList = _context.Categories
     .Select(c => new SelectListItem
     {
         Value = c.CategoryId.ToString(),
         Text = isArabic ? c.CNameArb : c.CName
     }).ToList();

            // 🔁 Send filters back to view
            ViewBag.FilterName = name;
            ViewBag.FilterCategory = category;
            ViewBag.FilterPrice = price;
            ViewBag.FilterStatus = status;

            return View(pagedModel);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var product = _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefault(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            var specifications = _context.Specification
       .Where(s => s.ProductId == id)
       .ToList();

            var viewModel = new EditProductViewModel
            {
                ProductId = product.ProductId,
                Name = product.Name,
                NameArb = product.NameArb,
                Description = product.Description,
                DescriptionArb = product.DescriptionArb,
                Ds_Barcode = product.Ds_Barcode,   // shown in view but not editable in backend
                Ds_ProdID = product.Ds_ProdID,
                CoastPrice = product.CoastPrice,
                Price = product.Price,
                CoastPriceStr = product.CoastPrice.ToString("F3", CultureInfo.InvariantCulture),
                PriceStr = product.Price.ToString("F3", CultureInfo.InvariantCulture),
              
                Stock =
                    (_context.ProductStocks.Where(s => s.ProductId == product.ProductId)
                        .Sum(s => (int?)s.StockQty) ?? 0)
                    - (_context.OrderItems.Where(o => o.ProductId == product.ProductId)
                        .Sum(o => (int?)o.Quantity) ?? 0),
                Weight = product.Weight,
                CategoryId = product.CategoryId,
                SubCategoryId = product.SubCategoryId,
                brandid = product.BrandId,
                Units = product.Units,
                Warranty = product.Warranty,
                ProductsModel = product.ProductsModel,
                SKUId = product.SKUId,
                IsActive = product.IsActive,
                ExistingImages = product.ProductImages
    .Select(img => new ProductImageViewModel
    {
        ImageId = img.ImageId,
        Base64Image = "data:" + img.ContentType + ";base64," +
                      Convert.ToBase64String(img.ImageData)
    }).ToList(),
                Specifications = specifications

            };

            // For dropdowns
            ViewBag.Categories = _context.Categories.Where(c => c.IsActive).ToList();
            ViewBag.SubCategories = _context.SubCategories
                                           .Where(s => s.ParentCategoryId == product.CategoryId)
                                           .ToList();
            ViewBag.brand1 = _context.Brand
                                       .Select(c => new { c.BrandId, c.BrandNameAEn })
                                       .ToList();

            return View(viewModel);
        }
        [HttpPost]
        public IActionResult DeleteProductImage(int imageId)
        {
            var image = _context.ProductImages.FirstOrDefault(x => x.ImageId == imageId);

            if (image == null)
            {
                return Json(new { success = false, message = "Image not found" });
            }

            _context.ProductImages.Remove(image);
            _context.SaveChanges();

            return Json(new { success = true });
        }


        //[HttpPost]
        //public IActionResult Edit(EditProductViewModel model, List<IFormFile> NewImages)
        //{
        //    try
        //    {
        //        var product = _context.Products
        //                              .Include(p => p.ProductImages)
        //                              .FirstOrDefault(p => p.ProductId == model.ProductId);

        //        if (product == null)
        //            return NotFound();

        //        // ✅ Preload dropdowns (for validation fallback)
        //        ViewBag.Categories = _context.Categories.Where(c => c.IsActive).ToList();
        //        ViewBag.SubCategories = _context.SubCategories
        //                                        .Where(s => s.ParentCategoryId == model.CategoryId)
        //                                        .ToList();

        //        // ✅ Update product fields
        //        product.Name = model.Name;
        //        product.NameArb = model.NameArb;

        //        // ❌ Do NOT update barcode (keep unique)
        //        // product.Ds_Barcode = model.Ds_Barcode;

        //        product.Ds_ProdID = model.Ds_ProdID;
        //        product.Description = model.Description;
        //        product.DescriptionArb = model.DescriptionArb;
        //        product.Price = model.Price;
        //        product.Weight = model.Weight;
        //        product.CategoryId = model.CategoryId;
        //        product.SubCategoryId = model.SubCategoryId;
        //        product.brandid = model.brandid;

        //        product.ProductsModel = model.ProductsModel;
        //        product.Units = model.Units;
        //        product.Warranty = model.Warranty;
        //        product.SKUId = model.SKUId;
        //        product.IsActive = model.IsActive;
        //        product.CoastPrice = model.CoastPrice;
        //        product.UpdatedAt = DateTime.Now;

        //        // ✅ If new images are uploaded → delete old ones and replace
        //        if (NewImages != null && NewImages.Count > 0)
        //        {
        //            // Remove old images
        //            _context.ProductImages.RemoveRange(product.ProductImages);

        //            // Add new compressed images
        //            foreach (var img in NewImages)
        //            {
        //                if (img.Length > 0)
        //                {
        //                    using (var originalStream = img.OpenReadStream())
        //                    using (var originalImage = Image.FromStream(originalStream))
        //                    {
        //                        var compressedBytes = CompressImage(originalImage, 800, 800, 70L);

        //                        _context.ProductImages.Add(new ProductImages
        //                        {
        //                            ProductId = product.ProductId,
        //                            ImageData = compressedBytes,
        //                            ContentType = "image/jpeg",
        //                            IsPrimary = false
        //                        });
        //                    }
        //                }
        //            }
        //        }

        //        _context.SaveChanges();

        //        TempData["Success"] = "✅ Product updated successfully!";
        //        return RedirectToAction("ProductList");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ Error updating product: {ex}");

        //        TempData["Error"] = "❌ Something went wrong while updating the product. Please try again.";

        //        // Reload dropdowns before returning view
        //        ViewBag.Categories = _context.Categories.Where(c => c.IsActive).ToList();
        //        ViewBag.SubCategories = _context.SubCategories
        //                                        .Where(s => s.ParentCategoryId == model.CategoryId)
        //                                        .ToList();

        //        return View(model);
        //    }
        //}
        [HttpPost]
        public async Task<IActionResult> Edit(EditProductViewModel model, List<IFormFile> NewImages)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = _context.Products
                                      .Include(p => p.ProductImages)
                                      .FirstOrDefault(p => p.ProductId == model.ProductId);

                if (product == null)
                    return NotFound();

                // Dropdown preload
                ViewBag.Categories = _context.Categories.Where(c => c.IsActive).ToList();
                ViewBag.SubCategories = _context.SubCategories
                                                .Where(s => s.ParentCategoryId == model.CategoryId)
                                                .ToList();

                // Update product fields
                product.Name = model.Name;
                product.NameArb = model.NameArb;
                product.Ds_ProdID = model.Ds_ProdID;
                product.Description = model.Description;
                product.DescriptionArb = model.DescriptionArb;

                product.CoastPrice = decimal.Parse(model.CoastPriceStr, CultureInfo.InvariantCulture);
                product.Price = decimal.Parse(model.PriceStr, CultureInfo.InvariantCulture);
                product.Weight = model.Weight;
                product.CategoryId = model.CategoryId;
                product.SubCategoryId = model.SubCategoryId;
                product.BrandId = model.brandid;
                product.ProductsModel = model.ProductsModel;
                product.Units = model.Units;
                product.Warranty = model.Warranty;
                product.SKUId = model.SKUId;
                product.IsActive = model.IsActive;
                product.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync(); // update record first



                if (model.Specifications != null)
                {
                    // Delete old specifications
                    var oldSpecs = _context.Specification.Where(s => s.ProductId == product.ProductId).ToList();
                    _context.Specification.RemoveRange(oldSpecs);
                    await _context.SaveChangesAsync();

                    // Add new/edited specifications
                    foreach (var spec in model.Specifications)
                    {
                        spec.ProductId = product.ProductId;
                        spec.CreatedAt = DateTime.Now;
                        _context.Specification.Add(spec);
                    }
                    await _context.SaveChangesAsync();
                }


                // Image Upload Section
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };

                //if (NewImages != null && NewImages.Count > 0)
                //{
                //    // Delete old images
                //    var oldImages = _context.ProductImages.Where(i => i.ProductId == product.ProductId).ToList();
                //    _context.ProductImages.RemoveRange(oldImages);
                //    await _context.SaveChangesAsync();

                //    foreach (var file in NewImages)
                //    {
                //        var ext = Path.GetExtension(file.FileName).ToLower();

                //        if (!allowedExtensions.Contains(ext))
                //        {
                //            TempData["Error"] = "Only JPG, JPEG, PNG, BMP and WEBP files are allowed.";
                //            return View(model);   // Not redirect — maintain form state
                //        }

                //        // WebP direct save
                //        if (ext == ".webp")
                //        {
                //            using (var ms = new MemoryStream())
                //            {
                //                await file.CopyToAsync(ms);
                //                _context.ProductImages.Add(new ProductImages
                //                {
                //                    ProductId = product.ProductId,
                //                    ImageData = ms.ToArray(),
                //                    ContentType = "image/webp",
                //                    IsPrimary = false
                //                });
                //            }
                //            continue;
                //        }

                //        // Compress other formats
                //        using (var originalStream = file.OpenReadStream())
                //        using (var originalImage = Image.FromStream(originalStream))
                //        {
                //            var compressedBytes = CompressImage(originalImage, 800, 800, 70L);

                //            _context.ProductImages.Add(new ProductImages
                //            {
                //                ProductId = product.ProductId,
                //                ImageData = compressedBytes,
                //                ContentType = "image/jpeg",
                //                IsPrimary = false
                //            });
                //        }
                //    }

                //    await _context.SaveChangesAsync();   // Save images
                //}
                if (NewImages != null && NewImages.Count > 0)
                {
                    foreach (var file in NewImages)
                    {
                        var ext = Path.GetExtension(file.FileName).ToLower();

                        if (!allowedExtensions.Contains(ext))
                        {
                            TempData["Error"] = "Only JPG, JPEG, PNG, BMP and WEBP files are allowed.";
                            return View(model);
                        }

                        if (ext == ".webp")
                        {
                            using var ms = new MemoryStream();
                            await file.CopyToAsync(ms);

                            _context.ProductImages.Add(new ProductImages
                            {
                                ProductId = product.ProductId,
                                ImageData = ms.ToArray(),
                                ContentType = "image/webp",
                                IsPrimary = false
                            });
                        }
                        //else
                        //{
                        //    using var originalStream = file.OpenReadStream();
                        //    using var originalImage = Image.FromStream(originalStream);

                        //    var compressedBytes = CompressImage(originalImage, 800, 800, 70L);

                        //    _context.ProductImages.Add(new ProductImages
                        //    {
                        //        ProductId = product.ProductId,
                        //        ImageData = compressedBytes,
                        //        ContentType = "image/jpeg",
                        //        IsPrimary = false
                        //    });
                        //}
                        else
                        {
                            using var stream = file.OpenReadStream();
                            using var image = Image.FromStream(stream);

                            bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                            byte[] finalImage;

                            if (hasAlpha && ext == ".png")
                            {
                                // ✅ Transparent PNG
                                finalImage = ImageHelper.CompressPng(image, 800, 800);
                            }
                            else
                            {
                                // ✅ Normal image
                                finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                            }

                            _context.ProductImages.Add(new ProductImages
                            {
                                ProductId = product.ProductId,
                                ImageData = finalImage,
                                ContentType = ext == ".png" ? "image/png" : "image/jpeg",
                                IsPrimary = false
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                await transaction.CommitAsync();
                TempData["Success"] = "Product updated successfully!";
                return RedirectToAction("ProductList");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating product: {ex}");
                TempData["Error"] = "Something went wrong while updating the product.";

                return View(model);
            }
        }

        //[HttpGet]
        //public IActionResult Edit(int id)
        //{
        //    var product = _context.Products
        //        .Include(p => p.ProductImages)
        //        .FirstOrDefault(p => p.ProductId == id);

        //    if (product == null)
        //        return NotFound();

        //    var viewModel = new EditProductViewModel
        //    {
        //        ProductId = product.ProductId,
        //        Name = product.Name,
        //        NameArb = product.NameArb,
        //        Description = product.Description,
        //        DescriptionArb = product.DescriptionArb,
        //        Ds_Barcode = product.Ds_Barcode,
        //        Ds_ProdID = product.Ds_ProdID,
        //        Price = product.Price,
        //        CoastPrice = product.CoastPrice,
        //        Stock =
        //  (_context.ProductStocks.Where(s => s.ProductId == product.ProductId)
        //      .Sum(s => (int?)s.StockQty) ?? 0)
        //  - (_context.OrderItems.Where(o => o.ProductId == product.ProductId)
        //      .Sum(o => (int?)o.Quantity) ?? 0),
        //        Weight = product.Weight,
        //        CategoryId = product.CategoryId,
        //        SubCategoryId = product.SubCategoryId,
        //        IsActive = product.IsActive ,
        //        ExistingImages = product.ProductImages
        //            .Select(img => new ProductImageViewModel
        //            {
        //                ImageId = img.ImageId,
        //                Base64Image = "data:" + img.ContentType + ";base64," + Convert.ToBase64String(img.ImageData)
        //            }).ToList()
        //    };

        //    // For dropdowns
        //    ViewBag.Categories = _context.Categories.Where(c => c.IsActive).ToList();
        //    ViewBag.SubCategories = _context.SubCategories.Where(s => s.ParentCategoryId == product.CategoryId).ToList();

        //    return View(viewModel);
        //}
        //[HttpPost]
        //public IActionResult Edit(EditProductViewModel model, List<IFormFile> NewImages)
        //{
        //    try
        //    {
        //        var product = _context.Products
        //                              .Include(p => p.ProductImages)
        //                              .FirstOrDefault(p => p.ProductId == model.ProductId);

        //        if (product == null)
        //            return NotFound();

        //        // ✅ Preload dropdowns (still needed for validation fallback)
        //        ViewBag.Categories = _context.Categories.Where(c => c.IsActive).ToList();
        //        ViewBag.SubCategories = _context.SubCategories.Where(s => s.ParentCategoryId == model.CategoryId).ToList();

        //        // ✅ Update product fields
        //        product.Name = model.Name;
        //        product.NameArb = model.NameArb;
        //        product.Ds_Barcode = model.Ds_Barcode;
        //        product.Ds_ProdID = model.Ds_ProdID;
        //        product.Description = model.Description;
        //        product.DescriptionArb = model.DescriptionArb;
        //        product.Price = model.Price;

        //        product.Weight = model.Weight;
        //        product.CategoryId = model.CategoryId;
        //        product.SubCategoryId = model.SubCategoryId;
        //        product.IsActive=model.IsActive;
        //        product.CoastPrice = model.CoastPrice;
        //        product.UpdatedAt = DateTime.Now;

        //        // ✅ Handle NEW images with compression
        //        if (NewImages != null && NewImages.Count > 0)
        //        {
        //            foreach (var img in NewImages)
        //            {
        //                if (img.Length > 0)
        //                {
        //                    // ✅ Open uploaded file as Image and compress
        //                    using (var originalStream = img.OpenReadStream())
        //                    using (var originalImage = Image.FromStream(originalStream))
        //                    {
        //                        // Compress + resize (max 800px, 70% JPEG quality)
        //                        var compressedBytes = CompressImage(originalImage, 800, 800, 70L);

        //                        _context.ProductImages.Add(new ProductImages
        //                        {
        //                            ProductId = product.ProductId,
        //                            ImageData = compressedBytes,      // ✅ compressed image
        //                            ContentType = "image/jpeg",       // ✅ force JPEG after compression
        //                            IsPrimary = false
        //                        });
        //                    }
        //                }
        //            }
        //        }

        //        _context.SaveChanges();

        //        TempData["Success"] = "✅ Product updated successfully!";
        //        return RedirectToAction("ProductList");
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log error (replace with ILogger)
        //        Console.WriteLine($"❌ Error updating product: {ex}");

        //        TempData["Error"] = "❌ Something went wrong while updating the product. Please try again.";

        //        // Reload dropdowns before returning view
        //        ViewBag.Categories = _context.Categories.Where(c => c.IsActive).ToList();
        //        ViewBag.SubCategories = _context.SubCategories.Where(s => s.ParentCategoryId == model.CategoryId).ToList();

        //        return View(model);
        //    }
        //}

        // ✅ Create GET
        public IActionResult Create()
        {
            // Get only Active Categories
            ViewBag.Categories = _context.Categories
                                         .Where(c => c.IsActive)
                                         .Select(c => new { c.CategoryId, c.CName })
                                         .ToList();
            ViewBag.brand1 = _context.Brand
                                         .Select(c => new { c.BrandId, c.BrandNameAEn })
                                         .ToList();

            return View();
        }

        // ✅ AJAX for SubCategories
        [HttpGet]
        public JsonResult GetSubCategories(int categoryId)
        {
            var subcategories = _context.SubCategories
                                        .Where(sub => sub.ParentCategoryId == categoryId)
                                        .Select(sub => new { sub.SubCategoryId, sub.SCName })
                                        .ToList();

            return Json(subcategories);
        }
        [HttpPost]
        public async Task<IActionResult> Create(ProductViewModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
              
                if (!string.IsNullOrEmpty(model.Ds_Barcode))
                {
                    bool barcodeExists = await _context.Products
                        .AnyAsync(p => p.Ds_Barcode == model.Ds_Barcode);

                    if (barcodeExists)
                    {
                        ModelState.AddModelError("Ds_Barcode", "This barcode is already used. Please enter a unique barcode.");
                        ViewBag.Categories = _context.Categories.Where(c => c.IsActive).ToList();
                        ViewBag.Brand1 = _context.Brand.ToList();
                        return View(model);
                    }
                }
                // ✅ Allowed Image Extensions
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };

                // Validate All Images First
                if (model.ProductImages != null && model.ProductImages.Count > 0)
                {
                    foreach (var file in model.ProductImages)
                    {
                        var ext = Path.GetExtension(file.FileName).ToLower();
                        if (!allowedExtensions.Contains(ext))
                        {
                            TempData["Error"] = "Only JPG, JPEG, PNG, BMP and WEBP files are allowed.";
                            ViewBag.Categories = _context.Categories.Where(c => c.IsActive).ToList();
                            ViewBag.Brand1 = _context.Brand.ToList();
                            return View(model);
                        }
                    }
                }


                var product = new Products
                {
                    CategoryId = model.CategoryId,
                    BrandId = model.brandid,
                    SubCategoryId = model.SubCategoryId,
                    Ds_Barcode = model.Ds_Barcode,
                    Ds_ProdID = model.Ds_ProdID,
                    Name = model.Name,
                    NameArb = model.NameArb,
                    Description = model.Description,
                    DescriptionArb = model.DescriptionArb,
                    Price = model.Price,
                    CoastPrice = model.CoastPrice,
                    Stock = model.Stock,  
                    Weight = model.Weight,
                    SKUId = model.SKUId,
                    Warranty = model.Warranty,
                    Units = model.Units,
                    ProductsModel = model.ProductsModel,
                    CreatedAt = DateTime.Now,
                    CreatedBy = 1 
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                
                var stockEntry = new ProductStock
                {
                    ProductId = product.ProductId,
                    StockQty = model.Stock,
                    CreatedAt = DateTime.Now,
                    CreatedBy = 1 
                };

                _context.ProductStocks.Add(stockEntry);
                await _context.SaveChangesAsync();

                // Save Images with Compression
                // Save Images with Compression
                //if (model.ProductImages != null && model.ProductImages.Count > 0)
                //{
                //    foreach (var file in model.ProductImages)
                //    {
                //        var ext = Path.GetExtension(file.FileName).ToLower();

                //        // WEBP ko alag handle
                //        if (ext == ".webp")
                //        {
                //            using (var ms = new MemoryStream())
                //            {
                //                await file.CopyToAsync(ms);
                //                _context.ProductImages.Add(new ProductImages
                //                {
                //                    ProductId = product.ProductId,
                //                    ImageData = ms.ToArray(),
                //                    ContentType = "image/webp",
                //                    IsPrimary = false
                //                });
                //            }
                //            continue;
                //        }

                //        // Other image formats
                //        using (var originalStream = file.OpenReadStream())
                //        using (var originalImage = Image.FromStream(originalStream))
                //        {
                //            var compressedBytes = CompressImage(originalImage, 800, 800, 70L);

                //            _context.ProductImages.Add(new ProductImages
                //            {
                //                ProductId = product.ProductId,
                //                ImageData = compressedBytes,
                //                ContentType = "image/jpeg",
                //                IsPrimary = false
                //            });
                //        }
                //    }

                //    // <<—— YEH MISSING THA
                //    await _context.SaveChangesAsync();
                //}
                // Save Images with Smart Compression
                if (model.ProductImages != null && model.ProductImages.Count > 0)
                {
                    foreach (var file in model.ProductImages)
                    {
                        var ext = Path.GetExtension(file.FileName).ToLower();

                        // ✅ WEBP → direct save
                        if (ext == ".webp")
                        {
                            using var ms = new MemoryStream();
                            await file.CopyToAsync(ms);

                            _context.ProductImages.Add(new ProductImages
                            {
                                ProductId = product.ProductId,
                                ImageData = ms.ToArray(),
                                ContentType = "image/webp",
                                IsPrimary = false
                            });

                            continue;
                        }

                        // ✅ Other formats (JPG, PNG, BMP)
                        using var stream = file.OpenReadStream();
                        using var image = Image.FromStream(stream);

                        bool hasAlpha = Image.IsAlphaPixelFormat(image.PixelFormat);

                        byte[] finalImage;

                        if (hasAlpha && ext == ".png")
                        {
                            // ✅ Transparent PNG
                            finalImage = ImageHelper.CompressPng(image, 800, 800);
                        }
                        else
                        {
                            // ✅ Normal image → JPG compress
                            finalImage = ImageHelper.CompressImage(image, 800, 800, 70L);
                        }

                        _context.ProductImages.Add(new ProductImages
                        {
                            ProductId = product.ProductId,
                            ImageData = finalImage,
                            ContentType = ext == ".png" ? "image/png" : "image/jpeg",
                            IsPrimary = false
                        });
                    }

                    await _context.SaveChangesAsync();
                }

                if (model.Specifications != null && model.Specifications.Count > 0)
                {
                   
                    foreach (var item in model.Specifications)
                    {
                        item.ProductId = product.ProductId;
                        item.CreatedAt = DateTime.Now;

                        _context.Specification.Add(item);
                    }

                    _context.SaveChanges();
                }

                await transaction.CommitAsync();

                TempData["Success"] = "Product created successfully!";
                return RedirectToAction("ProductList");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating product: {ex}");
                TempData["Error"] = "Something went wrong while creating the product.";
                return RedirectToAction("ProductList");
            }
        }

        

        [HttpPost]
        public IActionResult DeleteProduct(int id)
        {
            try
            {
                var product = _context.Products
                                      .Include(p => p.ProductImages)
                                      .FirstOrDefault(p => p.ProductId == id);

                if (product == null)
                    return Json(new { success = false, message = "Product not found!" });

                // ✅ Delete images too
                _context.ProductImages.RemoveRange(product.ProductImages);
                _context.Products.Remove(product);
                _context.SaveChanges();

                return Json(new { success = true, message = "Product deleted successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting product: {ex.Message}");
                return Json(new { success = false, message = "Something went wrong while deleting the product." });
            }
        }

        private byte[] CompressImage(Image image, int maxWidth, int maxHeight, long quality)
        {
            // ✅ Resize proportionally
            int newWidth = image.Width;
            int newHeight = image.Height;

            if (image.Width > maxWidth || image.Height > maxHeight)
            {
                double widthRatio = (double)maxWidth / image.Width;
                double heightRatio = (double)maxHeight / image.Height;
                double ratio = Math.Min(widthRatio, heightRatio);

                newWidth = (int)(image.Width * ratio);
                newHeight = (int)(image.Height * ratio);
            }

            using (var resizedBitmap = new Bitmap(image, newWidth, newHeight))
            using (var ms = new MemoryStream())
            {
                // ✅ Get JPEG encoder
                var jpegEncoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

                // ✅ Set compression quality
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);

                // ✅ Save compressed image
                resizedBitmap.Save(ms, jpegEncoder, encoderParams);

                return ms.ToArray();
            }
        }
        [HttpGet]
        public async Task<JsonResult> AutoCompleteSql(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            IQueryable<Products> queryable = _context.Products;

            // Detect if input is Arabic
            bool isArabic = term.Any(c => c >= 0x0600 && c <= 0x06FF);

            if (isArabic)
            {
                queryable = queryable.Where(c => c.NameArb.Contains(term));
            }
            else
            {
                queryable = queryable.Where(c => c.Name.Contains(term));
            }
            var suggestions = await queryable
                .Select(c => isArabic ? c.NameArb : c.Name)
                .Take(10)
                .ToListAsync();
            return Json(suggestions);
        }

    }

}
