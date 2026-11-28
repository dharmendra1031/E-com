using DocumentFormat.OpenXml.Spreadsheet;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using MF.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Reporting.NETCore;
using Microsoft.ReportingServices.ReportProcessing.ReportObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using static EcommerceWebApplication.Controllers.ReportController;
using static EcommerceWebApplication.Controllers.UserAccountController;
using static EcommerceWebApplication.Controllers.WebCheckoutController;

namespace EcommerceWebApplication.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class EcommerceApiController : ControllerBase
    {
        private readonly EcommerceDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IPaymentService _paymentService;

        private readonly IWebHostEnvironment _env;
        public EcommerceApiController(EcommerceDbContext context, IEmailService emailService, IPaymentService paymentService, IWebHostEnvironment env)
        {
            _context = context;
            _emailService = emailService;
            _paymentService = paymentService;
            _env = env;
        }


        private int? CurrentCustomerId(System.Security.Claims.ClaimsPrincipal? principal = null)
        {
            principal ??= User;
            if (!CustomerApiAuth.TryGetUserId(principal, out var id)) return null;
            var hash = _context.Customers.AsNoTracking()
                .Where(u => u.UserId == id && u.IsActive && u.IsDeleted == 0 &&
                    u.CustType == "Customer" && u.Role != null && u.Role.IsActive && u.Role.RoleName == "Customer")
                .Select(u => u.PasswordHash).SingleOrDefault();
            return hash != null && CustomerApiAuth.IsCurrent(principal, hash) ? id : null;
        }

        private static bool ValidEmail(string? email) => !string.IsNullOrWhiteSpace(email) &&
            email.Length <= 150 && new EmailAddressAttribute().IsValid(email.Trim());

        private async Task<IActionResult> SendCustomerOtp(string? email, bool reset, string lang)
        {
            if (!ValidEmail(email)) return BadRequest(new { message = "A valid email is required." });
            email = email!.Trim();
            var exists = await _context.Customers.AnyAsync(u => u.Email == email && u.CustType == "Customer" &&
                u.IsDeleted == 0 && (!reset || u.IsActive));
            if (exists != reset)
                return Ok(new { success = true, message = "If eligible, a code will be sent." });

            var key = CustomerPassword.ApiOtpKey(email, reset);
            var now = DateTime.Now;
            var otp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var rows = await _context.UserOtps.Where(x => x.Email == key).Take(2).ToListAsync();
            if (rows.Count > 1) return Conflict(new { message = "Unable to issue verification code." });
            if (rows.Count == 1 && rows[0].Expiry > now.AddMinutes(4))
                return StatusCode(429, new { message = "Please retry in one minute." });
            var row = rows.SingleOrDefault();
            if (row == null)
            {
                row = new UserOtp { Email = key, Otp = otp };
                _context.UserOtps.Add(row);
            }
            row.Otp = otp;
            row.IsVerified = false;
            row.Expiry = now.AddMinutes(5);
            _emailService.Send(email, lang == "ar" ? "رمز التحقق" : "Verification code",
                $"<p>{otp}</p><p>{(lang == "ar" ? "صالح لمدة خمس دقائق" : "Valid for five minutes.")}</p>");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { success = true, message = "If eligible, a code will be sent." });
        }

        private async Task<bool> ConsumeCustomerOtp(string email, string otp, bool reset)
        {
            var key = CustomerPassword.ApiOtpKey(email, reset);
            var now = DateTime.Now;
            var rows = await _context.UserOtps.Where(x => x.Email == key).Take(2).ToListAsync();
            if (rows.Count != 1 || rows[0].IsVerified || rows[0].Expiry <= now || rows[0].Otp != otp)
                return false;
            _context.UserOtps.Remove(rows[0]);
            return true;
        }

        private async Task<IActionResult> ResetCustomerPassword(string? email, string? otp, string? password, string lang)
        {
            if (!ValidEmail(email) || !CustomerPassword.IsValidNew(password) || string.IsNullOrWhiteSpace(otp))
                return BadRequest(new { message = "Valid email, OTP and password required." });
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var matches = await _context.Customers.Where(u => u.Email == email!.Trim() && u.IsActive && u.IsDeleted == 0 &&
                u.CustType == "Customer" && u.Role != null && u.Role.IsActive && u.Role.RoleName == "Customer").Take(2).ToListAsync();
            if (matches.Count != 1 || !await ConsumeCustomerOtp(email!, otp, true))
                return BadRequest(new { message = "Invalid or expired OTP." });
            matches[0].PasswordHash = CustomerPassword.Hash(password!);
            matches[0].UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { success = true, status = true, message = lang == "ar" ? "تم تحديث كلمة المرور" : "Password reset successfully." });
        }

        private async Task<int?> OptionalCustomerIdAsync()
        {
            var result = await HttpContext.AuthenticateAsync(CustomerApiAuth.Scheme);
            return result.Succeeded ? CurrentCustomerId(result.Principal) : null;
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Where(categories => categories.IsActive)
                .Select(c => new
                {
                    c.CategoryId,
                    c.CName,
                    c.CNameArb,
                    c.CDescription,
                    c.IsActive,
                    c.CreatedAt,
                    CategoryImageBase64 = c.CategoryImage != null
                        ? "data:image/png;base64," + Convert.ToBase64String(c.CategoryImage)
                        : null
                })
                .ToListAsync();

            return Ok(categories);
        }
        [HttpGet]
        public IActionResult GetAllBanners()
        {
            var banners = _context.Banner
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new
                {
                    x.BannerId,
                    x.Title,
                    x.TitleArabic,
                    x.DisplayOrder,
                    ImageBase64 = x.BannerImage != null
                        ? "data:image/png;base64," + Convert.ToBase64String(x.BannerImage)
                        : null
                })
                .ToList();

            return Ok(banners);
        }


        [HttpGet]
        public async Task<IActionResult> GetSubCategories([FromQuery] int? categoryId)
        {
            var query = _context.SubCategories
                .Include(sc => sc.ParentCategory)
                .Where(sc => sc.ParentCategory.IsActive)
                .AsQueryable();


            if (categoryId.HasValue)
                query = query.Where(sc => sc.ParentCategoryId == categoryId.Value);

            var subCategories = await query
                .Where(subCategories => subCategories.IsActive)
                .Select(sc => new
                {
                    sc.SubCategoryId,
                    sc.ParentCategoryId,
                    ParentCategoryName = sc.ParentCategory != null ? sc.ParentCategory.CName : "N/A",
                    sc.SCName,
                    sc.SCNameArb,
                    sc.SCDescription,
                    sc.SCDescriptionArb,
                    SubCategoryImageBase64 = sc.SubCategoryImage != null
                        ? "data:image/jpeg;base64," + Convert.ToBase64String(sc.SubCategoryImage)
                        : null,
                    sc.IsActive,
                    sc.CreatedAt
                })
                .ToListAsync();

            return Ok(subCategories);
        }
        //[HttpGet]
        //public async Task<IActionResult> GetProducts([FromQuery] int? categoryId, [FromQuery] int? subCategoryId, int PageIndex = 1, int PageSize = 10)
        //{
        //    var query = _context.Products
        //     .Include(p => p.ProductImages)
        //     .Include(p => p.Category)
        //     .Include(p => p.SubCategory)
        //     .Where(p => p.IsActive && p.Category.IsActive && p.SubCategory.IsActive)   // ✅ Only active Category & SubCategory
        //     .AsQueryable();

        //    // ✅ Filter by Category
        //    if (categoryId.HasValue)
        //        query = query.Where(p => p.CategoryId == categoryId.Value);

        //    // ✅ Filter by SubCategory
        //    if (subCategoryId.HasValue)
        //        query = query.Where(p => p.SubCategoryId == subCategoryId.Value);

        //    var products = await query
        //        .Select(p => new
        //        {
        //            p.ProductId,
        //            p.Name,
        //            p.NameArb,
        //            p.DescriptionArb,
        //            p.Description,
        //            p.Price,
        //            p.CategoryId,
        //            p.SubCategoryId,
        //            p.CreatedAt,
        //            p.IsActive,

        //            // ✅ Total stock from ProductStock table
        //            TotalStock = _context.ProductStocks
        //                .Where(s => s.ProductId == p.ProductId)
        //                .Sum(s => (int?)s.StockQty) ?? 0,


        //            TotalSoldStock = _context.OrderItems
        //                .Where(o => o.ProductId == p.ProductId)
        //                .Sum(o => (int?)o.Quantity) ?? 0,


        //            Stock =
        //                (_context.ProductStocks.Where(s => s.ProductId == p.ProductId)
        //                    .Sum(s => (int?)s.StockQty) ?? 0)
        //                - (_context.OrderItems.Where(o => o.ProductId == p.ProductId)
        //                    .Sum(o => (int?)o.Quantity) ?? 0),

        //            // ✅ First product image
        //            imageBase64 = p.ProductImages
        //                .Select(img => "data:" + img.ContentType + ";base64," +
        //                               Convert.ToBase64String(img.ImageData))
        //                .FirstOrDefault(),

        //            // ✅ Popularity info
        //            TotalReviews = _context.ProductReviews.Count(r => r.ProductId == p.ProductId),
        //            AverageRating = _context.ProductReviews
        //                .Where(r => r.ProductId == p.ProductId)
        //                .Average(r => (double?)r.Rating) ?? 0
        //        })
        //        .ToListAsync();

        //    return Ok(products);
        //}

        //[HttpGet]
        //public async Task<IActionResult> GetTopProductsByCategory()
        //{
        //    var categories = await _context.Categories
        //        .Where(c => c.IsActive)
        //        .Select(c => new
        //        {
        //            c.CategoryId,
        //            c.CName,
        //            c.CNameArb
        //        })
        //        .ToListAsync();

        //    var result = new List<object>();

        //    foreach (var cat in categories)
        //    {
        //        // Products in this category
        //        var productsQuery = _context.Products
        //            .Include(p => p.ProductImages)
        //            .Include(p => p.SubCategory)
        //            .Where(p => p.IsActive && p.CategoryId == cat.CategoryId)
        //            .AsQueryable();

        //        // Products sorted by reviews
        //        var productsWithReviews = await productsQuery
        //            .Select(p => new
        //            {
        //                p.ProductId,
        //                p.Name,
        //                p.NameArb,
        //                p.Description,
        //                p.DescriptionArb,
        //                p.Price,
        //                TotalReviews = _context.ProductReviews.Count(r => r.ProductId == p.ProductId),
        //                AverageRating = _context.ProductReviews
        //                    .Where(r => r.ProductId == p.ProductId)
        //                    .Average(r => (double?)r.Rating) ?? 0,
        //                imageBase64 = p.ProductImages
        //                    .Select(img => "data:" + img.ContentType + ";base64," + Convert.ToBase64String(img.ImageData))
        //                    .FirstOrDefault()
        //            })
        //            .OrderByDescending(p => p.TotalReviews)
        //            .Take(10)
        //            .ToListAsync();

        //        // If no reviews, take first 2 products
        //        var finalProducts = productsWithReviews.Any()
        //            ? productsWithReviews
        //            : await productsQuery
        //                .Select(p => new
        //                {
        //                    p.ProductId,
        //                    p.Name,
        //                    p.NameArb,
        //                    p.Description,
        //                    p.DescriptionArb,
        //                    p.Price,
        //                    TotalReviews = 0,
        //                    AverageRating = 0.0, // ✅ make it double
        //                    imageBase64 = p.ProductImages
        //                        .Select(img => "data:" + img.ContentType + ";base64," + Convert.ToBase64String(img.ImageData))
        //                        .FirstOrDefault()
        //                })
        //                .Take(2)
        //                .ToListAsync();

        //        result.Add(new
        //        {
        //            cat.CategoryId,
        //            cat.CName,
        //            cat.CNameArb,
        //            Products = finalProducts
        //        });
        //    }

        //    return Ok(result);
        //}

        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] int? categoryId, [FromQuery] int? subCategoryId, int? UserId, string? Deviceid, int PageIndex = 1, int PageSize = 12,
                string? sortBy = "newest")
        {
            var actor = await OptionalCustomerIdAsync();
            var cartProductIds = actor.HasValue && actor == UserId
                ? await _context.CartItems.Where(c => c.UserId == actor && c.VendorListingId == null).Select(c => c.ProductId).ToListAsync()
                : new List<int>();

            var query = _context.Products
     .Include(p => p.ProductImages)
     .Where(p => p.IsActive)
     .AsQueryable();
            //var query = _context.Products
            //    .Include(p => p.ProductImages)
            //    .Include(p => p.Category)
            //    .Include(p => p.SubCategory)
            //    .Where(p => p.IsActive && p.Category.IsActive && p.SubCategory.IsActive)
            //    .AsQueryable();
            if (categoryId.HasValue && subCategoryId.HasValue)
            {
                // dono selected
                query = query.Where(p => p.CategoryId == categoryId && p.SubCategoryId == subCategoryId);
            }
            else if (categoryId.HasValue)
            {
                // sirf category
                query = query.Where(p => p.CategoryId == categoryId);
            }
            else if (subCategoryId.HasValue)
            {
                // sirf subcategory
                query = query.Where(p => p.SubCategoryId == subCategoryId);
            }


            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);


            if (subCategoryId.HasValue)
                query = query.Where(p => p.SubCategoryId == subCategoryId.Value);

            query = sortBy switch
            {
                "low-high" => query.OrderBy(p => p.Price),
                "high-low" => query.OrderByDescending(p => p.Price),
                "popular" => query.OrderByDescending(p =>
                    _context.ProductReviews.Count(r => r.ProductId == p.ProductId)),
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };


            var totalCount = await query.CountAsync();


            var products = await query
     .Join(_context.Brand,
           p => p.BrandId,
           b => b.BrandId,
           (p, b) => new { Product = p, Brand = b })
     .Skip((PageIndex - 1) * PageSize)
     .Take(PageSize)
     .Select(x => new
     {
         x.Product.ProductId,
         x.Product.Name,
         x.Product.NameArb,
         x.Product.DescriptionArb,
         x.Product.Description,
         x.Product.Price,
         x.Product.CategoryId,
         x.Product.SubCategoryId,
         x.Product.CreatedAt,
         x.Product.IsActive,
         x.Product.ProductsModel,
         x.Product.Warranty,
         x.Product.Units,
         x.Product.SKUId,

         BrandName = x.Brand.BrandNameAEn,   // <-- Brand Name here

         TotalStock = _context.ProductStocks
             .Where(s => s.ProductId == x.Product.ProductId)
             .Sum(s => (int?)s.StockQty) ?? 0,

         TotalSoldStock = _context.OrderItems
             .Where(o => o.ProductId == x.Product.ProductId)
             .Sum(o => (int?)o.Quantity) ?? 0,

         Stock =
             (_context.ProductStocks.Where(s => s.ProductId == x.Product.ProductId)
                 .Sum(s => (int?)s.StockQty) ?? 0)
             - (_context.OrderItems.Where(o => o.ProductId == x.Product.ProductId)
                 .Sum(o => (int?)o.Quantity) ?? 0),

         imageBase64 = x.Product.ProductImages
             .Select(img => "data:" + img.ContentType + ";base64," +
                            Convert.ToBase64String(img.ImageData))
             .FirstOrDefault(),

         TotalReviews = _context.ProductReviews.Count(r => r.ProductId == x.Product.ProductId),
         AverageRating = _context.ProductReviews
             .Where(r => r.ProductId == x.Product.ProductId)
             .Average(r => (double?)r.Rating) ?? 0,
         IsInCart = cartProductIds.Contains(x.Product.ProductId)


     })
     .ToListAsync();



            return Ok(new
            {
                TotalCount = totalCount,
                PageIndex,
                PageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize),
                Data = products,
                //categoryId = p.categoryId
                CategoryId = categoryId
            });
        }


        [HttpGet]
        public async Task<IActionResult> GetTopRatedProducts()
        {
            var topProducts = await _context.ProductReviews
                .GroupBy(r => r.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    AverageRating = g.Average(r => r.Rating),
                    TotalReviews = g.Count()
                })
                .OrderByDescending(x => x.AverageRating)
                .ThenByDescending(x => x.TotalReviews)
                .Take(3)
                .Join(
                    _context.Products,
                    review => review.ProductId,
                    product => product.ProductId,
                    (review, product) => new
                    {
                        product.ProductId,
                        product.Name,
                        review.AverageRating,
                        review.TotalReviews
                    }
                )
                .ToListAsync();

            return Ok(topProducts);
        }
        //[HttpGet]
        //public IActionResult DealSummary(int wdId)
        //{
        //    var deal = _context.WekendDeals
        //        .Where(w => w.WdId == wdId)
        //        .Select(w => new
        //        {
        //            // Weekend Deal Table Data
        //            WdId = w.WdId,
        //            WdNameEn = w.WdNameEn,
        //            WdNameAr = w.WdNameAr,
        //            ProductId = w.ProductId,
        //            DiscPer = w.DiscPer,

        //            FromDate = w.FromDate.ToString("yyyy-MM-dd"),
        //            ToDate = w.ToDate.ToString("yyyy-MM-dd"),
        //            MaxQuantity = w.MaxQuantity,

        //            // Product Data + Discount Calculation
        //            Product = _context.Products
        //                .Where(p => p.ProductId == w.ProductId)
        //                .Select(p => new
        //                {
        //                    ProductId = p.ProductId,
        //                    NameEn = p.Name,
        //                    NameAr = p.NameArb,

        //                    Price = p.Price,

        //                    DiscountPercent = w.DiscPer,
        //                    DiscountAmount = (p.Price * w.DiscPer) / 100,
        //                    OfferPrice = p.Price - ((p.Price * w.DiscPer) / 100),

        //                    // First Product Image Only
        //                    //ImageUrl = p.ProductImages
        //                    //    .OrderBy(i => i.ImageId)
        //                    //    .Select(i => i.ProductImages)
        //                    //    .FirstOrDefault() ?? ""
        //                })
        //                .FirstOrDefault()
        //        })
        //        .FirstOrDefault();

        //    if (deal == null)
        //        return NotFound(new { message = "Deal not found" });

        //    return Ok(deal);
        //}
        [HttpGet]
        public IActionResult GetWeekendDeals(int productId)
        {
            var deals = (from d in _context.WekendDeals
                         join p in _context.Products on d.ProductId equals p.ProductId
                         where d.ProductId == productId    // 🔥 Only specific product ID
                         select new WeekendDealProductVM
                         {
                             DealId = d.WdId,
                             WdNameAr = d.WdNameAr,
                             WdNameEn = d.WdNameEn,

                             FromDate = d.FromDate,
                             ToDate = d.ToDate,

                             ProductId = p.ProductId,
                             ProductName = p.Name,
                             ProductNameAr = p.NameArb,
                             ProductPrice = p.Price,
                             DiscPer = d.DiscPer,
                             // Discount Amount
                             DiscountAmount = Math.Round((p.Price * d.DiscPer) / 100, 3),

                             // Final Price After Discount
                             OfferPrice = Math.Round(p.Price - ((p.Price * d.DiscPer) / 100), 3),

                             // Multiple Images
                             ProductImagesBase64 = p.ProductImages
                                .Select(img => Convert.ToBase64String(img.ImageData))
                                .ToList()
                         }).FirstOrDefault();     // 🔥 Only one record return

            if (deals == null)
                return NotFound(new { message = "No Weekend Deal found for this product" });

            return Ok(deals);
        }



        //[HttpPost]
        //public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        //{
        //    if (_context.Customers.Any(u => u.Email == request.Email))
        //        return BadRequest(new { message = "Email already exists" });

        //    // ✅ Hash Password
        //    string passwordHash = request.Password; //ComputeSha256Hash(request.Password);

        //    var newUser = new Customers
        //    {
        //        FullName = request.FullName,
        //        FullNameArb = request.FullNameArb ?? request.FullName,
        //        Email = request.Email,
        //        Phone = request.Phone,
        //        PasswordHash = passwordHash,
        //        RoleId = 2,         // ✅ always RoleId = 2
        //        IsActive = true,
        //        CreatedAt = DateTime.Now
        //    };

        //    _context.Customers.Add(newUser);
        //    await _context.SaveChangesAsync();

        //    return Ok(new { message = "User registered successfully", userId = newUser.UserId });
        //}


        [HttpPost]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("CustomerAuth")]
        public async Task<IActionResult> Login([FromBody] LoginDto model, string lang = "en")
        {

            bool isArabic = lang?.ToLower() == "ar";
            var user = await _context.Customers.FirstOrDefaultAsync(u => u.Email == model.Email && u.CustType == "Customer" &&
                u.IsDeleted == 0 && u.Role != null && u.Role.IsActive && u.Role.RoleName == "Customer");


            if (user == null || !CustomerPassword.Verify(user.PasswordHash, model.Password, out var upgrade))
            {
                return Unauthorized(new
                {
                    message = isArabic
                        ? "البريد الإلكتروني أو كلمة المرور غير صالحة"
                        : "Invalid email or password"
                });
            }

            if (!user.IsActive)
            {
                return Unauthorized(new
                {
                    message = isArabic
                        ? "الحساب غير نشط. تواصل مع الدعم."
                        : "Account is inactive. Contact support."
                });
            }
            if (upgrade)
            {
                user.PasswordHash = CustomerPassword.Hash(model.Password);
                await _context.SaveChangesAsync();
            }
            var totalCartCount = _context.CartItems
                                 .Count(x => x.UserId == user.UserId);

            var tokenOptions = HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
                .Get(CustomerApiAuth.Scheme);
            var ticket = CustomerApiAuth.CreateTicket(user.UserId, user.PasswordHash!, DateTimeOffset.UtcNow, tokenOptions.BearerTokenExpiration);
            return Ok(new
            {
                accessToken = tokenOptions.BearerTokenProtector.Protect(ticket),
                tokenType = "Bearer",
                expiresIn = (int)tokenOptions.BearerTokenExpiration.TotalSeconds,
                userId = user.UserId,
                fullName = user.FullName,
                email = user.Email,
                phone = user.Phone,
                roleId = user.RoleId,
                totalcart = totalCartCount
            });
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> GetProfile([FromQuery] int userId)
        {
            if (CurrentCustomerId() is not int actor || actor != userId) return Unauthorized();
            var user = await _context.Customers.FirstOrDefaultAsync(u => u.UserId == userId && u.IsDeleted == 0);
            if (user == null) return NotFound();

            string profileBase64 = null;


            if (user.ProfileImage != null && user.ProfileImage.Length > 0)
            {
                profileBase64 = "data:image/png;base64," + Convert.ToBase64String(user.ProfileImage);
            }

            return Ok(new
            {
                user.UserId,
                user.FullName,
                user.FullNameArb,
                user.Email,
                user.Phone,
                user.BirthDate,
                user.RoleId,
                user.IsActive,
                user.CreatedAt,
                ProfileImageBase64 = profileBase64
            });
        }


        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto model, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor || actor != model.UserId) return Unauthorized();
            bool isArabic = lang?.ToLower() == "ar";

            var user = await _context.Customers
                .FirstOrDefaultAsync(u => u.UserId == model.UserId && u.IsDeleted == 0);

            if (user == null)
            {
                return NotFound(new
                {
                    message = isArabic ? "المستخدم غير موجود" : "User not found"
                });
            }

            user.FullName = model.FullName ?? user.FullName;
            user.FullNameArb = model.FullNameArb ?? user.FullNameArb;
            user.Phone = model.Phone ?? user.Phone;
            user.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = isArabic ? "تم تحديث الملف الشخصي بنجاح" : "Profile updated successfully"
            });
        }


        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public IActionResult ChangePassword([FromBody] ChangePasswordRequest request, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor || request == null || actor != request.UserId) return Unauthorized();
            bool isArabic = lang?.ToLower() == "ar";

            if (request == null || !CustomerPassword.IsValidNew(request.NewPassword))
            {
                if (isArabic)
                    return BadRequest(new { message = "طلب غير صالح" });
                else
                    return BadRequest(new { message = "Invalid request" });
            }

            var user = _context.Customers
                .FirstOrDefault(u => u.UserId == request.UserId && u.IsDeleted == 0);

            if (user == null)
            {
                if (isArabic)
                    return NotFound(new { message = "لم يتم العثور على المستخدم" });
                else
                    return NotFound(new { message = "User not found" });
            }

            if (!CustomerPassword.Verify(user.PasswordHash, request.OldPassword, out _))
            {
                if (isArabic)
                    return BadRequest(new { message = "كلمة المرور القديمة غير صحيحة" });
                else
                    return BadRequest(new { message = "Old password is incorrect" });
            }

            user.PasswordHash = CustomerPassword.Hash(request.NewPassword);
            user.UpdatedAt = DateTime.Now;
            _context.SaveChanges();

            if (isArabic)
                return Ok(new { message = "تم تحديث كلمة المرور بنجاح!" });
            else
                return Ok(new { message = "Password updated successfully!" });
        }


        [HttpGet]
        public IActionResult GetProductsBySubCategory([FromQuery] string subCategory, string lang = "en")
        {
            var currentCulture = lang?.ToLower();
            try
            {
                if (string.IsNullOrWhiteSpace(subCategory))
                {
                    if (currentCulture == "ar")
                        return BadRequest("اسم الفئة الفرعية مطلوب.");
                    else
                        return BadRequest("Subcategory name is required.");

                }



                var subCat = _context.SubCategories
                    .FirstOrDefault(sc =>
                        sc.SCName.ToLower() == subCategory.ToLower() ||
                        sc.SCNameArb == subCategory
                    );

                if (subCat == null)
                {
                    if (currentCulture == "ar")
                        return NotFound(new { message = "لم يتم العثور على الفئة الفرعية" });
                    else
                        return NotFound(new { message = "Subcategory not found" });

                }

                var products = _context.Products
       .Where(p => p.SubCategoryId == subCat.SubCategoryId)
       .Join(_context.Brand,
             p => p.BrandId,
             b => b.BrandId,
             (p, b) => new { Product = p, Brand = b })
       .Select(x => new
       {
           x.Product.ProductId,
           x.Product.Name,
           x.Product.NameArb,
           x.Product.Description,
           x.Product.DescriptionArb,
           x.Product.Price,
           x.Product.Warranty,
           x.Product.ProductsModel,
           x.Product.Units,
           x.Product.SKUId,

           BrandName = x.Brand.BrandNameAEn,   // ⭐ ADD BRAND NAME

           TotalStock = _context.ProductStocks
               .Where(s => s.ProductId == x.Product.ProductId)
               .Sum(s => (int?)s.StockQty) ?? 0,

           TotalSoldStock = _context.OrderItems
               .Where(o => o.ProductId == x.Product.ProductId)
               .Sum(o => (int?)o.Quantity) ?? 0,

           Stock =
               (_context.ProductStocks.Where(s => s.ProductId == x.Product.ProductId)
                   .Sum(s => (int?)s.StockQty) ?? 0)
               - (_context.OrderItems.Where(o => o.ProductId == x.Product.ProductId)
                   .Sum(o => (int?)o.Quantity) ?? 0),

           ImageBase64 = x.Product.ProductImages
               .Select(img => "data:image/png;base64," + Convert.ToBase64String(img.ImageData))
               .FirstOrDefault() ?? "/assets/img/product/placeholder.png"
       })
       .ToList();

                return Ok(products);
            }
            catch (Exception ex)
            {
                var message = currentCulture == "ar" ? "حدث خطأ ما!" : "Something went wrong!";
                return StatusCode(500, new { message, error = ex.Message });
            }
        }
        public class ProductSpecificationDto
        {
            public string? SpeciNameAr { get; set; }
            public string? SpeciValueAr { get; set; }
            public string? SpeciNameEn { get; set; }
            public string? SpeciValueEn { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDetails(int productId, int? UserId, string? Deviceid)
        {
            var actor = await OptionalCustomerIdAsync();
            bool isInCart = actor.HasValue && actor == UserId &&
                await _context.CartItems.AnyAsync(c => c.UserId == actor && c.VendorListingId == null && c.ProductId == productId);

            ProductDetailsDto product = null;


            var connectionString = _context.Database.GetConnectionString();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using var command = new SqlCommand("GetProductDetailsWithRating", connection);
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@ProductId", productId);

                using var reader = command.ExecuteReader();


                if (reader.Read())
                {
                    product = new ProductDetailsDto
                    {
                        ProductId = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        NameArb = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        DescriptionArb = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Price = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                        Stock = reader.GetInt32(6),
                        Weight = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                        //AverageRating = reader.IsDBNull(8) ? 0 : reader.GetDouble(8),
                        AverageRating = reader.IsDBNull(8) ? 0 : Convert.ToDouble(reader[8]),
                        TotalReviews = reader.GetInt32(9),


                        // ⭐ New Added Fields
                        Warranty = reader.IsDBNull(10) ? "" : reader.GetString(10),
                        ProductsModel = reader.IsDBNull(11) ? "" : reader.GetString(11),
                        Units = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                        SKUId = reader.IsDBNull(13) ? "" : reader.GetString(13),
                        BrandName = reader.IsDBNull(14) ? "" : reader.GetString(14),


                        Images = new List<ProductImageDto>(),
                        Specifications = new List<ProductSpecificationDto>()
                    };
                }


                if (reader.NextResult() && product != null)
                {
                    while (reader.Read())
                    {
                        product.Images.Add(new ProductImageDto
                        {
                            ImageId = reader.GetInt32(0),
                            ProductId = reader.GetInt32(1),
                            IsPrimary = reader.GetBoolean(2),
                            Base64 = reader.IsDBNull(3) ? "" : reader.GetString(3)
                        });
                    }
                }
                if (reader.NextResult() && product != null)
                {
                    while (reader.Read())
                    {
                        product.Specifications.Add(new ProductSpecificationDto
                        {
                            SpeciNameAr = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            SpeciValueAr = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            SpeciNameEn = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            SpeciValueEn = reader.IsDBNull(3) ? "" : reader.GetString(3)
                        });
                    }
                }
            }


            if (product != null)
            {
                product.Stock =
                    (_context.ProductStocks.Where(s => s.ProductId == productId)
                        .Sum(s => (int?)s.StockQty) ?? 0)
                    - (_context.OrderItems.Where(o => o.ProductId == productId)
                        .Sum(o => (int?)o.Quantity) ?? 0);

                product.IsInCart = isInCart;
            }

            return product != null ? Ok(product) : NotFound(new { message = "Product not found" });
        }

        [HttpGet]
        public async Task<IActionResult> search([FromQuery] string term, [FromQuery] string lang = "en")
        {
            if (string.IsNullOrWhiteSpace(term))
                return Ok(new List<object>());


            var products = await _context.Products
                .Where(p => p.Name.Contains(term) || p.NameArb.Contains(term)
                )
                .OrderBy(p => p.Name)
                .Take(6)
                .Select(p => new
                {
                    productId = p.ProductId,
                    name = lang == "ar" ? p.NameArb : p.Name,
                    description = lang == "ar" ? p.DescriptionArb : p.Description,
                    price = Math.Round(p.Price, 3),
                    categoryId = p.CategoryId,
                    subCategoryId = p.SubCategoryId,
                    createdAt = p.CreatedAt,

                    TotalStock = _context.ProductStocks
                        .Where(s => s.ProductId == p.ProductId)
                        .Sum(s => (int?)s.StockQty) ?? 0,

                    TotalSoldStock = _context.OrderItems
                        .Where(o => o.ProductId == p.ProductId)
                        .Sum(o => (int?)o.Quantity) ?? 0,

                    Stock =
                        (_context.ProductStocks.Where(s => s.ProductId == p.ProductId)
                            .Sum(s => (int?)s.StockQty) ?? 0)
                        - (_context.OrderItems.Where(o => o.ProductId == p.ProductId)
                            .Sum(o => (int?)o.Quantity) ?? 0),

                    imageBase64 = p.ProductImages
                        .OrderBy(img => img.ImageId)
                        .Select(img => "data:" + img.ContentType + ";base64," + Convert.ToBase64String(img.ImageData))
                        .FirstOrDefault(),

                    totalReviews = _context.ProductReviews.Count(r => r.ProductId == p.ProductId),
                    averageRating = _context.ProductReviews
                        .Where(r => r.ProductId == p.ProductId)
                        .Average(r => (double?)r.Rating) ?? 0
                })
                .ToListAsync();

            return Ok(products);
        }




        [HttpGet]
        public IActionResult GetProductReviews(int productId)
        {
            var reviews = _context.ProductReviews
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.ReviewId,
                    r.Rating,
                    r.Comment,
                    UserName = _context.Customers
                        .Where(u => u.UserId == r.UserId && u.IsDeleted == 0)
                        .Select(u => u.FullName)
                        .FirstOrDefault(),
                    r.CreatedAt
                })
                .ToList();

            return Ok(reviews);
        }




        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> UploadProfilePic([FromForm] IFormFile profilePic, [FromQuery] int userId, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor || actor != userId) return Unauthorized();
            //var currentCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var currentCulture = lang?.ToLower();

            if (profilePic == null || profilePic.Length == 0)
            {
                if (currentCulture == "ar")
                    return BadRequest("لم يتم تحميل أي ملف");
                else
                    return BadRequest("No file uploaded");
            }


            if (profilePic.Length > 100 * 1024)
            {
                if (currentCulture == "ar")
                    return BadRequest("يجب ألا يتجاوز حجم الصورة ١٠٠ كيلوبايت");
                else
                    return BadRequest("Image size must be <= 100KB");

            }


            using var ms = new MemoryStream();
            await profilePic.CopyToAsync(ms);
            var imageBytes = ms.ToArray();

            var user = await _context.Customers.FindAsync(userId);
            if (user == null)
            {
                if (currentCulture == "ar")
                    return NotFound("لم يتم العثور على المستخدم");
                else
                    return NotFound("User not found");
            }


            user.ProfileImage = imageBytes;
            await _context.SaveChangesAsync();

            if (currentCulture == "ar")
            {
                return Ok(new { message = "تم تحديث صورة الملف الشخصي" });
            }
            else
                return Ok(new { message = "Profile picture updated" });


        }

        //[HttpGet]
        //public async Task<IActionResult> GetUserAddresses([FromQuery] int userId)
        //{
        //    var addresses = await _context.UserAddresses
        //        .Where(a => a.UserId == userId)
        //        .OrderByDescending(a => a.CreatedAt)
        //        .ToListAsync();

        //    return Ok(addresses);
        //}
        [HttpGet]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> GetUserAddresses([FromQuery] int userId)
        {
            if (CurrentCustomerId() is not int actor || actor != userId) return Unauthorized();
            var addresses = await (
                from ua in _context.UserAddresses
                join ar in _context.Area on ua.Area equals ar.Id.ToString() into areaJoin
                from ar in areaJoin.DefaultIfEmpty()
                join bl in _context.Block on ua.Block equals bl.Id.ToString() into blockJoin
                from bl in blockJoin.DefaultIfEmpty()
                where ua.UserId == userId && ua.isdeleted == 0
                orderby ua.CreatedAt descending
                select new
                {
                    ua.AddressId,
                    ua.UserId,
                    ua.Title,
                    ua.HouseNo,
                    ua.Street,
                    ua.Floor,
                    ua.Landmark,
                    ua.Governorate,
                    ua.City,
                    ua.State,
                    ua.ZipCode,
                    ua.Country,
                    ua.Phone,
                    ua.IsDefault,
                    ua.CreatedAt,
                    ua.Area,
                    ua.Block,

                    AreaName = ar.AreaName,
                    AreaNameArb = ar.AreaNameArb,
                    BlockName = bl.BlockName,
                    BlockNameArb = bl.BlockNameArb
                }
            ).ToListAsync();

            return Ok(addresses);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> SaveAddress([FromBody] UserAddress model)
        {
            if (CurrentCustomerId() is not int actor || actor != model.UserId) return Unauthorized();

            if (!string.IsNullOrEmpty(model.Phone))
            {
                string phone = model.Phone.Trim();


                if (phone.StartsWith("+965"))
                {
                    phone = phone.Substring(4);
                }

                else if (phone.StartsWith("965"))
                {
                    phone = phone.Substring(3);
                }


                phone = new string(phone.Where(char.IsDigit).ToArray());


                if (phone.Length >= 8)
                {
                    phone = phone.Substring(phone.Length - 8);
                }


                if (model.AddressId == 0)
                {

                    model.Title = model.Title;
                    model.Phone = phone;
                    model.isdeleted = 0;
                    _context.UserAddresses.Add(model);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Update existing
                    //var existing = await _context.UserAddresses.FindAsync(model.AddressId);
                    var existing = await _context.UserAddresses
           .FirstOrDefaultAsync(a => a.AddressId == model.AddressId && a.UserId == actor && a.isdeleted == 0);
                    if (existing == null) return NotFound("Address not found");


                    existing.Title = model.Title;
                    existing.Area = model.Area;
                    existing.Block = model.Block;
                    existing.Street = model.Street;
                    existing.HouseNo = model.HouseNo;
                    existing.Floor = model.Floor;
                    existing.Landmark = model.Landmark;
                    existing.Governorate = model.Governorate;
                    existing.City = model.City;
                    existing.State = model.State;
                    existing.ZipCode = model.ZipCode;
                    existing.Country = model.Country;
                    existing.Phone = phone;
                    existing.IsDefault = model.IsDefault;
                    existing.isdeleted = 0;

                    await _context.SaveChangesAsync();
                    model = existing; // for returning be
                }
            }
            return Ok(model);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> DeleteAddress(int id, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor) return Unauthorized();
            var currentCulture = lang?.ToLower();
            //var currentCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var addr = await _context.UserAddresses.FirstOrDefaultAsync(a => a.AddressId == id && a.UserId == actor && a.isdeleted == 0);
            if (addr == null)
            {
                if (currentCulture == "ar")
                {
                    return NotFound(new { message = "العنوان غير موجود" });
                }
                else
                {
                    return NotFound(new { message = "Address not found" });
                }

            }


            addr.isdeleted = 1;
            _context.UserAddresses.Update(addr);
            await _context.SaveChangesAsync();

            if (currentCulture == "ar")
            {
                return Ok(new { message = "تم حذف العنوان" });
            }
            else
            {
                return Ok(new { message = "Address deleted" });
            }

        }

        [HttpGet]

        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> GetUserOrders([FromQuery] int userId)
        {
            if (CurrentCustomerId() is not int actor || actor != userId) return Unauthorized();

            var orders = await _context.Orders
                .Where(o => o.CustomerId == userId
                         && (o.PaymentStatus == 1 || o.PaymentStatus == 2))
                .OrderByDescending(o => o.CreatedAt)
                .Join(
                    _context.PaymentStatus,
                    o => o.PaymentStatus,
                    st => st.PaymentstatusId,
                    (o, st) => new
                    {
                        o.OrderId,
                        o.CreatedAt,
                        o.TotalAmount,
                        // Payment status names from joined table
                        PaymentStatusName = st.PaymentstatusName,
                        PaymentStatusNameArb = st.PaymentStatusNameArb,
                        o.UpdatedAt,
                        o.DeliveryCharge,
                        Status = (o.OrderStatus != null
                                  ? o.OrderStatus.OrderstatusName + " / " + o.OrderStatus.OrderStatusNameArb
                                  : ""),
                        StatusCode = o.OrderStatus != null ? o.OrderStatus.OrderstatusName : "",
                        paytype = o.PaymentType,
                        ReturnRequest = o.ReturnRequest != null
                                        ? new
                                        {
                                            o.ReturnRequest.RequestId,
                                            o.ReturnRequest.RequestType,
                                            o.ReturnRequest.Status,
                                            o.ReturnRequest.RequestedAt
                                        }
                                        : null
                    })
                .ToListAsync();

            return Ok(orders);
        }
        //[HttpGet("GetUserOrders")]
        //public async Task<IActionResult> GetUserOrders([FromQuery] int userId)
        //{
        //    var culture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
        //    bool isArabic = culture == "ar";

        //    var ordersRaw = await _context.Orders
        //        .Where(o => o.CustomerId == userId && (o.PaymentStatus == 1 || o.PaymentStatus == 2))
        //        .OrderByDescending(o => o.CreatedAt)
        //        .Include(o => o.OrderStatus)
        //        .Include(o => o.ReturnRequest)
        //        .ToListAsync();

        //    var paymentStatuses = await _context.PaymentStatus.ToListAsync();

        //    var result = new List<object>();

        //    foreach (var o in ordersRaw)
        //    {
        //        var payStatus = paymentStatuses.FirstOrDefault(p => p.PaymentstatusId == o.PaymentStatus);

        //        // ✅ Generate Invoice PDF
        //        var pdfBytes = await GenerateInvoicePdf(o.OrderId, isArabic);
        //        string pdfBase64 = pdfBytes != null ? Convert.ToBase64String(pdfBytes) : null;

        //        result.Add(new
        //        {
        //            o.OrderId,
        //            o.CreatedAt,
        //            o.TotalAmount,
        //            PaymentStatusName = payStatus?.PaymentstatusName,
        //            PaymentStatusNameArb = payStatus?.PaymentStatusNameArb,
        //            o.UpdatedAt,
        //            o.DeliveryCharge,
        //            Status = o.OrderStatus != null
        //                ? o.OrderStatus.OrderstatusName + " / " + o.OrderStatus.OrderStatusNameArb
        //                : "",
        //            StatusCode = o.OrderStatus?.OrderstatusName,
        //            paytype = o.PaymentType,

        //            // 🔥 INVOICE PDF HERE
        //            InvoicePdfBase64 = pdfBase64,

        //            ReturnRequest = o.ReturnRequest != null
        //                ? new
        //                {
        //                    o.ReturnRequest.RequestId,
        //                    o.ReturnRequest.RequestType,
        //                    o.ReturnRequest.Status,
        //                    o.ReturnRequest.RequestedAt
        //                }
        //                : null
        //        });
        //    }

        //    return Ok(result);
        //}

        //public async Task<IActionResult> GetOrderInvoice([FromQuery] int orderId)
        //{
        //    try
        //    {
        //        var culture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
        //        bool isArabic = culture == "ar";

        //        var pdfBytes = await GenerateInvoicePdf(orderId, isArabic);

        //        if (pdfBytes == null)
        //            return NotFound("Invoice data not found.");

        //        var fileName = $"OrderInvoice_{orderId}.pdf";

        //        return File(pdfBytes, "application/pdf", fileName);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, "Error generating invoice: " + ex.Message);
        //    }
        //}
        [HttpGet]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> GetOrderInvoice(int orderId, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor) return Unauthorized();
            if (!await _context.Orders.AnyAsync(o => o.OrderId == orderId && o.CustomerId == actor)) return NotFound();
            try
            {
                bool isArabic = lang?.ToLower() == "ar";

                var pdfBytes = await GenerateInvoicePdf(orderId, isArabic);

                if (pdfBytes == null)
                    return NotFound("Invoice data not found.");

                var fileName = $"OrderInvoice_{orderId}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error generating invoice: " + ex.Message);
            }
        }

        private async Task<byte[]> GenerateInvoicePdf(int orderId, bool isArabic)
        {
            var rows = await _context.Set<OrderProductViewModel>()
                .FromSqlRaw("EXEC dbo.sp_GetOrderProducts @OrderId = {0}", orderId)
                .ToListAsync();

            if (rows == null || !rows.Any())
                return null;

            if (isArabic)
                rows.ForEach(r => r.ProductName = r.ProductNameArb);

            var rdlcFile = isArabic ? "OrderInvoiceReport_arbic.rdlc" : "OrderInvoiceReport.rdlc";
            var rdlcPath = Path.Combine(_env.ContentRootPath, "Reports", rdlcFile);

            using var defStream = System.IO.File.OpenRead(rdlcPath);
            var report = new LocalReport();
            report.EnableExternalImages = true;
            report.LoadReportDefinition(defStream);
            report.DataSources.Add(new ReportDataSource("DataSet1", rows));

            var company = _context.CompanyDetails.FromSqlRaw("SELECT TOP 1 * FROM CompanyDetails").FirstOrDefault();

            string imagePath = "";
            if (company?.Logo?.Length > 0)
            {
                var folder = Path.Combine(_env.WebRootPath, "Temp");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var filePath = Path.Combine(folder, "logo.png");
                System.IO.File.WriteAllBytes(filePath, company.Logo);
                imagePath = new Uri(filePath).AbsoluteUri;
            }

            report.SetParameters(new[]
            {
        new ReportParameter("pGeneratedAt", DateTime.Now.ToString("dd-MM-yyyy HH:mm")),
        new ReportParameter("CompanyName", isArabic ? company?.CompanyNameArabic : company?.CompanyName),
        new ReportParameter("Address", isArabic ? company?.AddressArabic : company?.Address),
        new ReportParameter("Phone", company?.Phone),
        new ReportParameter("Logo", imagePath),
        new ReportParameter("UserName", "Customer")
    });

            return report.Render("PDF");
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> GetUserReturnRequests([FromQuery] int userId)
        {
            if (CurrentCustomerId() is not int actor || actor != userId) return Unauthorized();
            var requests = await _context.ReturnRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestedAt)
                .Select(r => new
                {
                    r.RequestId,
                    r.OrderId,
                    r.RequestType,
                    r.Status,
                    r.RequestedAt,
                    OrderDate = r.Order.CreatedAt,
                    OrderStatus = r.Order.OrderStatus.OrderstatusName
                })
                .ToListAsync();

            return Ok(requests);
        }




        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> RequestReturnReplace([FromBody] ReturnRequestDto req)
        {
            if (CurrentCustomerId() is not int actor || req == null || actor != req.UserId) return Unauthorized();
            var currentCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

            if (req == null || req.OrderId <= 0 || req.UserId <= 0 || string.IsNullOrEmpty(req.RequestType))
            {
                if (currentCulture == "ar")
                {
                    return BadRequest("طلب غير صالح");
                }
                else
                {
                    return BadRequest("Invalid request");
                }
            }

            // ✅ Fetch order
            var order = await _context.Orders
                .Include(o => o.OrderStatus)
                .FirstOrDefaultAsync(o => o.OrderId == req.OrderId && o.CustomerId == req.UserId);

            if (order == null)
            {
                if (currentCulture == "ar")
                    return BadRequest("الطلب غير موجود");
                else
                    return BadRequest("Order not found");

            }


            // ✅ Only allow return/replace if Delivered
            if (order.OrderStatus.OrderstatusName != "Delivered")
            {
                if (currentCulture == "ar")
                    return BadRequest("يمكن إرجاع أو استبدال الطلبات التي تم تسليمها فقط.");
                else
                    return BadRequest("Only delivered orders can be returned or replaced");
            }


            // ✅ Ensure UpdatedAt (delivery date) is within 5 days
            if (order.UpdatedAt == null)
            {
                if (currentCulture == "ar")
                    return BadRequest("تاريخ التسليم غير موجود.");
                else
                    return BadRequest("Delivery date not found");
            }


            var daysSinceDelivery = (DateTime.Now - order.UpdatedAt).TotalDays;
            if (daysSinceDelivery > 5)
            {
                if (currentCulture == "ar")
                    return BadRequest("انتهت صلاحية طلب الإرجاع/الاستبدال – بعد مضي أكثر من 5 أيام من التسليم");
                else
                    return BadRequest("Return/Replace request expired (more than 5 days after delivery)");
            }



            var entity = new ReturnRequest
            {
                OrderId = req.OrderId,
                UserId = req.UserId,
                RequestType = req.RequestType,
                Reason = req.Reason ?? "No reason provided",
                Status = "Pending",
                RequestedAt = DateTime.Now
            };

            _context.ReturnRequests.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"{req.RequestType} request submitted successfully" });
        }

        [HttpPost]





        [HttpGet]
        public async Task<IActionResult> GetTopCategories()
        {
            var categories = await _context.TopCategories
                .Where(tc => tc.CategoryId != null)
                .Select(tc => new
                {
                    tc.TopCategoryId,
                    tc.CategoryId,
                    tc.Title,
                    tc.TitleArbic,
                    tc.SubTitleArbic,
                    tc.SubTitle,
                    ImageBase64 = tc.TopCategoryImage != null
                        ? "data:image/jpeg;base64," + Convert.ToBase64String(tc.TopCategoryImage)
                        : null
                })
                .ToListAsync();

            return Ok(categories);
        }


        [HttpGet]
        public async Task<IActionResult> GetCompanyDetails(string lang = "en")
        {
            //var currentCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var currentCulture = lang?.ToLower();

            var details = await _context.CompanyDetails
                .OrderByDescending(c => c.CompanyId)
                .FirstOrDefaultAsync();

            if (details == null)
            {
                if (currentCulture == "ar")
                {
                    return NotFound("لم يتم العثور على تفاصيل الشركة");
                }
                else
                {
                    return NotFound("Company details not found");
                }

            }


            var result = new
            {
                details.CompanyName,
                details.CompanyNameArabic,
                details.AddressArabic,
                details.Address,
                details.Email,
                details.Phone,
                details.FacebookUrl,
                details.TwitterUrl,
                details.InstagramUrl,
                details.Vat,
                details.QuantityLimit,
                details.DeliveryCharge,
                details.Heading,
                LogoBase64 = details.Logo != null ? $"data:image/png;base64,{Convert.ToBase64String(details.Logo)}" : null,
                //heading = true
            };

            return Ok(result);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetNotificationCount()
        {
            int unseenCount = await _context.Orders.CountAsync(o => o.NewNotification == 0);

            return Ok(new
            {
                unseenCount
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationsAsSeen()
        {

            var maxOrderId = await _context.Orders
                .Where(o => o.NewNotification == 0)
                .MaxAsync(o => (int?)o.OrderId) ?? 0;

            if (maxOrderId > 0)
            {
                var unseenOrders = await _context.Orders
                    .Where(o => o.OrderId <= maxOrderId && o.NewNotification == 0)
                    .ToListAsync();

                foreach (var ord in unseenOrders)
                {
                    ord.NewNotification = 1;
                }

                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                message = "Notifications marked as seen"
            });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetNewOrders()
        {
            var newOrders = await _context.Orders
                .Where(o => o.NewNotification == 0)
                .OrderByDescending(o => o.OrderId)
                .Join(_context.Customers,
                      order => order.CustomerId,
                      customer => customer.UserId,
                      (order, customer) => new
                      {
                          order.OrderId,
                          FullName = customer.FullName,
                          order.TotalAmount,
                          order.CreatedAt,
                          order.PaymentType
                      })
                .ToListAsync();

            return Ok(newOrders);
        }




        [HttpGet]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public IActionResult GetCart(int? userId, string? deviceId, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor || actor != userId) return Unauthorized();
            var cart = from c in _context.CartItems
                       join p in _context.Products on c.ProductId equals p.ProductId
                       join s in _context.ProductStocks on p.ProductId equals s.ProductId into stockGroup
                       where c.UserId == actor && c.VendorListingId == null
                       select new
                       {
                           c.ProductId, c.ProductName, c.ProductNameArb, p.Price, c.Quantity, c.ImageBase64,
                           Stock = (stockGroup.Sum(x => (int?)x.StockQty) ?? 0) -
                               (_context.OrderItems.Where(o => o.ProductId == p.ProductId).Sum(o => (int?)o.Quantity) ?? 0)
                       };
            return Ok(cart.ToList());
        }


        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public IActionResult SaveCart([FromBody] CartSaveRequest request, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor || request == null || actor != request.UserId) return Unauthorized();
            var currentCulture = lang?.ToLower();
            try
            {

                if (request.ProductId <= 0 || request.Quantity <= 0)
                {
                    return BadRequest(new
                    {
                        message = currentCulture == "ar"
                            ? "منتج غير صالح"
                            : "Invalid product"
                    });
                }
                var productData = (from p in _context.Products
                                   join s in _context.ProductStocks
                                        on p.ProductId equals s.ProductId into stockGroup
                                   where p.ProductId == request.ProductId && p.IsActive
                                   select new
                                   {
                                       p.ProductId,
                                       Name = p.Name ?? "",
                                       NameArb = p.NameArb ?? "",
                                       Price = p.Price,

                                       Stock = (stockGroup.Sum(x => (int?)x.StockQty) ?? 0) -
                                               (_context.OrderItems
                                               .Where(o => o.ProductId == p.ProductId)
                                               .Sum(o => (int?)o.Quantity) ?? 0),

                                       Image = _context.ProductImages
                                               .Where(i => i.ProductId == p.ProductId)
                                               .OrderByDescending(i => i.IsPrimary)
                                               .Select(i => new { i.ImageData, i.ContentType })
                                               .FirstOrDefault()
                                   }).FirstOrDefault();

                if (productData == null)
                {
                    return BadRequest(new
                    {
                        message = currentCulture == "ar"
                            ? "المنتج غير موجود"
                            : "Product not found"
                    });
                }

                int newQty = request.Quantity;

                var existingItem = _context.CartItems.FirstOrDefault(c => c.UserId == actor && c.VendorListingId == null && c.ProductId == request.ProductId);

                if (newQty > productData.Stock)
                {
                    return BadRequest(new
                    {
                        success = false,
                        notStock = true,
                        message = currentCulture == "ar"
                            ? $"الكمية المتوفرة في المخزون هي {productData.Stock} فقط"
                            : $"Only {productData.Stock} item(s) available in stock"
                    });
                }

                string imageBase64 = null;
                if (productData.Image != null && productData.Image.ImageData != null)
                {
                    imageBase64 = $"data:{productData.Image.ContentType};base64,{Convert.ToBase64String(productData.Image.ImageData)}";
                }

                if (existingItem != null)
                {

                    existingItem.Quantity = newQty;

                }
                else
                {

                    _context.CartItems.Add(new CartItem
                    {
                        UserId = actor,
                        DeviceId = string.Empty,
                        ProductId = productData.ProductId,
                        ProductName = productData.Name,
                        ProductNameArb = productData.NameArb,
                        Price = productData.Price,
                        Quantity = newQty,
                        ImageBase64 = imageBase64,
                        CreatedAt = DateTime.Now
                    });
                }

                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = currentCulture == "ar"
          ? "تمت إضافة المنتج إلى السلة بنجاح"
          : "Cart added successfully"
                });

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }




        public class UpdateCartQtyRequest
        {
            public int? UserId { get; set; }
            public string? DeviceId { get; set; }

            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public IActionResult UpdateCartQuantity([FromBody] UpdateCartQtyRequest request, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor || request == null || actor != request.UserId) return Unauthorized();
            var currentCulture = lang?.ToLower();

            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = currentCulture == "ar"
                        ? "طلب غير صالح"
                        : "Invalid request"
                });
            }


            


            // 🔍 Find product
            var productData = (from p in _context.Products
                               join s in _context.ProductStocks
                                    on p.ProductId equals s.ProductId into stockGroup
                               where p.ProductId == request.ProductId
                               select new
                               {
                                   Stock = (stockGroup.Sum(x => (int?)x.StockQty) ?? 0) -
                                           (_context.OrderItems
                                            .Where(o => o.ProductId == p.ProductId)
                                            .Sum(o => (int?)o.Quantity) ?? 0)
                               }).FirstOrDefault();
            if (productData == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = currentCulture == "ar"
                        ? "المنتج غير موجود"
                        : "Product not found"
                });
            }


            int availableStock = productData.Stock;

            // 🛒 Find cart item
            var cartItem = _context.CartItems.FirstOrDefault(c =>
                c.ProductId == request.ProductId &&
                c.UserId == actor && c.VendorListingId == null
            );

            if (cartItem == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = currentCulture == "ar"
                        ? "العنصر غير موجود في السلة"
                        : "Item not found in cart"
                });
            }


            int existingQty = cartItem.Quantity;
            int finalQty = request.Quantity; // +1 / -1

            // ❌ Minimum qty = 1
            if (finalQty < 1)
            {
                return Ok(new
                {
                    success = false,
                    quantity = existingQty
                });
            }

            // ❌ Stock exceed
            if (finalQty > availableStock)
            {
                return Ok(new
                {
                    success = false,
                    notStock = true,
                    message = currentCulture == "ar"
                        ? $"يتوفر فقط {availableStock} عنصر/عناصر في المخزون"
                        : $"Only {availableStock} item(s) available in stock",
                    availableStock = availableStock,
                    quantity = existingQty
                });
            }

            // ✅ Update only
            cartItem.Quantity = request.Quantity;
            _context.SaveChanges();


            return Ok(new
            {
                success = true,
                message = currentCulture == "ar"
        ? "تم تحديث السلة بنجاح"
        : "Cart updated successfully",
                productId = request.ProductId,
                quantity = finalQty
            });

        }

        public class DeleteCartItemRequest
        {
            public int? UserId { get; set; }
            public string? DeviceId { get; set; }
            public int ProductId { get; set; }
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public IActionResult DeleteCartItem([FromBody] DeleteCartItemRequest request, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor || request == null || actor != request.UserId) return Unauthorized();
            var currentCulture = lang?.ToLower();
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = currentCulture == "ar"
                        ? "طلب غير صالح"
                        : "Invalid request"
                });
            }


            


            var cartItem = _context.CartItems.FirstOrDefault(c =>
                c.ProductId == request.ProductId &&
                c.UserId == actor && c.VendorListingId == null
            );

            if (cartItem == null)
            {
                return Ok(new
                {
                    success = false,
                    message = currentCulture == "ar"
                        ? "العنصر غير موجود في السلة"
                        : "Item not found in cart"
                });
            }


            _context.CartItems.Remove(cartItem);
            _context.SaveChanges();

            return Ok(new
            {
                success = true,
                message = currentCulture == "ar"
         ? "تم إزالة المنتج من السلة بنجاح"
         : "Item removed from cart successfully",
                productId = request.ProductId
            });

        }




        //}
        [HttpPost]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("CustomerAuth")]
        public async Task<IActionResult> SendOtp([FromBody] OtpRequest request, string lang = "en")
        {
            return await SendCustomerOtp(request.email, false, lang);
        }


        // OTP-based registration DTO

        // Guest registration DTO

        //[HttpPost]
        //public async Task<IActionResult> VerifyOtpAndRegister([FromBody] RegisterRequest request, [FromQuery] string otp)
        //{
        //    using var transaction = await _context.Database.BeginTransactionAsync();
        //    try
        //    {
        //        // ✅ Check OTP
        //        var otpRecord = _context.UserOtps.FirstOrDefault(x => x.Email == request.Email);
        //        if (otpRecord == null || otpRecord.Otp != otp || otpRecord.Expiry < DateTime.Now)
        //            return BadRequest(new { message = "Invalid or expired OTP" });

        //        otpRecord.IsVerified = true;
        //        await _context.SaveChangesAsync();

        //        // ✅ Check if email exists
        //        if (_context.Customers.Any(u => u.Email == request.Email && u.CustType == "Customer"))
        //            return BadRequest(new { message = "Email already exists" });

        //        // ✅ Hash password (replace with real hashing)
        //        string passwordHash = request.Password;

        //        var newUser = new Customers
        //        {
        //            FullName = request.FullName,
        //            CustType = "Customer",
        //            FullNameArb = request.FullNameArb ?? request.FullName,
        //            Email = request.Email,
        //            Phone = request.Phone,
        //            PasswordHash = passwordHash,
        //            ShareholderNo = request.ShareholderNo,
        //            RoleId = 2, // Normal user
        //            IsActive = true,
        //            CreatedAt = DateTime.Now
        //        };

        //        _context.Customers.Add(newUser);
        //        await _context.SaveChangesAsync();

        //        await transaction.CommitAsync();
        //        return Ok(new { message = "User registered successfully", userId = newUser.UserId });
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        return StatusCode(500, new { message = "Something went wrong", error = ex.Message });
        //    }
        //}

        [HttpPost]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("CustomerAuth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, string lang = "en")
        {
            if (!ValidEmail(request.Email) || !CustomerPassword.IsValidNew(request.Password) ||
                string.IsNullOrWhiteSpace(request.Otp)) return BadRequest(new { message = "Valid email, password and OTP required." });
            request.Email = request.Email.Trim();
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {

                var currentCulture = lang?.ToLower();
                string emailExistsMsg = currentCulture == "ar" ? "البريد الإلكتروني موجود مسبقًا ." : "Email already exists";
                string phoneExistsMsg = currentCulture == "ar" ? "البريد الإلكتروني موجود مسبقًا ." : "Phone already exists";
                string userRegisteredMsg = currentCulture == "ar" ? "تم تسجيل المتخدم بنجاح." : "User registered successfully";
                string somethingWentWrongMsg = currentCulture == "ar" ? "حدث خطأ ما." : "Something went wrong";

                var existingUser = _context.Customers.FirstOrDefault(u =>
      (u.Email == request.Email || u.Phone == request.Phone)
      && u.CustType == "Customer");

                if (existingUser != null)
                {
                    if (existingUser.Email == request.Email)
                        return BadRequest(new { message = emailExistsMsg });

                    if (existingUser.Phone == request.Phone)
                        return BadRequest(new { message = phoneExistsMsg });
                }


                if (!await ConsumeCustomerOtp(request.Email, request.Otp, false))
                    return BadRequest(new { message = "Invalid or expired OTP." });

                string passwordHash = CustomerPassword.Hash(request.Password);

                var newUser = new Customers
                {
                    FullName = request.FullName,
                    CustType = "Customer",
                    FullNameArb = request.FullNameArb ?? request.FullName,
                    Email = request.Email,
                    Phone = request.Phone,
                    BirthDate = request.BirthDate,
                    PasswordHash = passwordHash,
                    ShareholderNo = request.ShareholderNo,
                    RoleId = 2,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    IsDeleted = 0
                };

                _context.Customers.Add(newUser);
                await _context.SaveChangesAsync();

                var totalCartCount = _context.CartItems
                                     .Count(x => x.UserId == newUser.UserId);

                await transaction.CommitAsync();
                return Ok(new
                {
                    message = userRegisteredMsg,
                    user = new
                    {
                        newUser.UserId,
                        newUser.FullName,
                        newUser.Email,
                        newUser.Phone,
                        newUser.FullNameArb,
                        newUser.ShareholderNo,
                        totalcartcount = totalCartCount
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var currentCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
                string somethingWentWrongMsg = currentCulture == "ar" ? "حدث خطأ ما." : "Something went wrong";
                return StatusCode(500, new { message = somethingWentWrongMsg, error = ex.Message });
            }
        }

        public class otpvarify
        {
            public string? email { get; set; }
            public string? mobile { get; set; }
            public string otp { get; set; }
        }
        //[HttpPost]
        //public async Task<IActionResult> VerifyOtpRegister([FromBody] otpvarify req, string lang = "en")
        //{
        //    try
        //    {
        //        var currentCulture = lang?.ToLower();
        //        string invalidOtpMsg = currentCulture == "ar" ? "كلمة مرور لمرة واحدة غير صالحة أو منتهية الصلاحية." : "Invalid or expired OTP";
        //        string successMsg = currentCulture == "ar" ? "تم التحقق من رمز التفعيل بنجاح." : "OTP verified successfully";

        //        var otpRecord = await _context.UserOtps.FirstOrDefaultAsync(x => x.Email == req.email || x.Mobile == req.mobile);

        //        if (otpRecord == null || otpRecord.Otp != req.otp || otpRecord.Expiry < DateTime.Now || otpRecord.IsVerified)
        //        {
        //            return BadRequest(new { message = invalidOtpMsg });
        //        }

        //        otpRecord.IsVerified = true;
        //        otpRecord.Expiry = DateTime.Now; 

        //        await _context.SaveChangesAsync();


        //        return Ok(new { message = successMsg });
        //    }
        //    catch (Exception ex)
        //    {
        //        var currentCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        //        string errorMsg = currentCulture == "ar" ? "حدث خطأ ما." : "Something went wrong";

        //        return StatusCode(500, new { message = errorMsg, error = ex.Message });
        //    }
        //}


        [HttpPost("verify-otp")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("CustomerAuth")]
        public async Task<IActionResult> VerifyOtpRegister([FromBody] otpvarify req, string lang = "en")
        {
            if (!ValidEmail(req.email) || string.IsNullOrWhiteSpace(req.otp)) return BadRequest();
            var key = CustomerPassword.ApiOtpKey(req.email!, false);
            var now = DateTime.Now;
            var matches = await _context.UserOtps.AsNoTracking().Where(x => x.Email == key && !x.IsVerified &&
                x.Expiry > now && x.Otp == req.otp).Take(2).CountAsync();
            return matches == 1 ? Ok(new { message = "OTP verified; submit it with registration." }) :
                BadRequest(new { message = "Invalid or expired OTP." });
        }
        //[HttpPost]
        //public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model, string lang = "en")
        //{
        //    try
        //    {
        //        var currentCulture = lang?.ToLower();

        //        string userNotFoundMsg = currentCulture == "ar" ? "المستخدم غير موجود" : "User not found";
        //        string successMsg = currentCulture == "ar" ? "تم إعادة تعيين كلمة المرور بنجاح" : "Password reset successfully";

        //        var user = await _context.Customers
        //            .Include(x => x.Role)
        //            .FirstOrDefaultAsync(x => x.Email == model.Email);

        //        if (user == null)
        //        {
        //            return BadRequest(new
        //            {
        //                status = false,
        //                message = userNotFoundMsg
        //            });
        //        }


        //        user.PasswordHash = model.NewPassword;
        //        user.UpdatedAt = DateTime.Now;

        //        await _context.SaveChangesAsync();

        //        return Ok(new
        //        {
        //            status = true,
        //            message = successMsg
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        var currentCulture = lang?.ToLower();
        //        string errorMsg = currentCulture == "ar" ? "حدث خطأ ما" : "Something went wrong";

        //        return StatusCode(500, new
        //        {
        //            status = false,
        //            message = errorMsg,
        //            error = ex.Message
        //        });
        //    }
        //}

        //[HttpPost("reset-password")]
        //public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model, string lang = "en")
        //{
        //    try
        //    {
        //        var currentCulture = lang?.ToLower();

        //        string userNotFoundMsg = currentCulture == "ar" ? "المستخدم غير موجود" : "User not found";
        //        string otpNotVerifiedMsg = currentCulture == "ar" ? "يرجى التحقق من رمز OTP أولاً" : "Please verify OTP first";
        //        string successMsg = currentCulture == "ar" ? "تم إعادة تعيين كلمة المرور بنجاح" : "Password reset successfully";

        //        bool isEmailFlow = !string.IsNullOrEmpty(model.Email);
        //        bool isMobileFlow = !string.IsNullOrEmpty(model.mobile);

        //        if (!isEmailFlow && !isMobileFlow)
        //        {
        //            return BadRequest(new { message = "Email or Mobile required" });
        //        }

        //        var user = await _context.Customers.FirstOrDefaultAsync(x =>
        //            (isEmailFlow && x.Email == model.Email) ||
        //            (isMobileFlow && x.Phone == model.mobile));

        //        if (user == null)
        //        {
        //            return BadRequest(new { status = false, message = userNotFoundMsg });
        //        }


        //        var otpRecord = await _context.UserOtps.FirstOrDefaultAsync(x =>
        //            (isEmailFlow && x.Email == model.Email) ||
        //            (isMobileFlow && x.Mobile == model.mobile));

        //        if (otpRecord == null || !otpRecord.IsVerified)
        //        {
        //            return BadRequest(new { status = false, message = otpNotVerifiedMsg });
        //        }


        //        user.PasswordHash = model.NewPassword; 
        //        user.UpdatedAt = DateTime.Now;


        //        //otpRecord.IsVerified = false;
        //        //otpRecord.Otp = null;

        //        await _context.SaveChangesAsync();

        //        return Ok(new
        //        {
        //            status = true,
        //            message = successMsg
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new
        //        {
        //            status = false,
        //            message = "Something went wrong",
        //            error = ex.Message
        //        });
        //    }
        //}
        [HttpPost("reset-password")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("CustomerAuth")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model, string lang = "en")
        {
            return await ResetCustomerPassword(model.Email, model.Otp, model.NewPassword, lang);
        }
        //[HttpPost("forgot-password")]
        //public async Task<IActionResult> SendOtpForPasswordReset([FromBody] OtpRequest request, string lang = "en")
        //{
        //    try
        //    {
        //        var currentCulture = lang?.ToLower();

        //        var user = _context.Customers.FirstOrDefault(u => u.Email == request.email && u.IsDeleted == 0);
        //        if (user == null)
        //        {
        //            if (currentCulture == "ar")
        //            {
        //                return BadRequest(new { message = "المستخدم الذي يحمل هذا البريد الإلكتروني غير موجود " });
        //            }
        //            else
        //            {
        //                return BadRequest(new { message = "User with this email does not exist" });
        //            }

        //        }
        //        // return BadRequest(new { message = "User with this email does not exist" });

        //        // ✅ Generate OTP
        //        var otp = new Random().Next(100000, 999999).ToString();
        //        string subject, body;

        //        if (currentCulture == "ar")
        //        {
        //            subject = "رمز التحقق لإعادة تعيين كلمة المرور - جمعية صباحية التعاونية";
        //            body = $@"
        //        <p>عزيزي {user.FullName},</p>
        //        <p>رمز التحقق الخاص بك لإعادة تعيين كلمة المرور هو:
        //        <strong>{otp}</strong></p>
        //        <p>صالح لمدة <strong>5 دقائق</strong>.</p>
        //        <p>يرجى عدم مشاركته مع أي شخص.</p>
        //        <br/>
        //        <p>شكرًا لك،<br/>فريق موقع جمعية صباحية التعاونية</p>";
        //        }
        //        else
        //        {
        //            subject = "Password Reset OTP - Raqmikart";
        //            body = $@"
        //        <p>Dear {user.FullName},</p>
        //        <p>Your One-Time Password (OTP) for resetting your password is:
        //        <strong>{otp}</strong></p>
        //        <p>This OTP is valid for <strong>5 minutes</strong>.</p>
        //        <p>Do not share it with anyone.</p>
        //        <br/>
        //        <p>Thank you,<br/>Team Raqmikart Website</p>";
        //        }

        //        //    string subject = "Password Reset OTP - Sabahiya Coop";
        //        //    string body = $@"
        //        //<p>Dear {user.FullName},</p>
        //        //<p>Your One-Time Password (OTP) for resetting your password is:
        //        //<strong>{otp}</strong></p>
        //        //<p>This OTP is valid for <strong>5 minutes</strong>.</p>
        //        //<p>Do not share it with anyone.</p>
        //        //<br/>
        //        //<p>Thank you,<br/>Team Shop Sabahiya Website</p>";


        //        //// ✅ Send email
        //        _emailService.Send(request.email, subject, body);

        //        // ✅ Save / update OTP record
        //        var existing = _context.UserOtps.FirstOrDefault(x => x.Email == request.email);
        //        if (existing != null)
        //        {
        //            existing.Otp = otp;
        //            existing.Expiry = DateTime.Now.AddMinutes(5);
        //            existing.IsVerified = false;
        //        }
        //        else
        //        {
        //            _context.UserOtps.Add(new UserOtp
        //            {
        //                Email = request.email,
        //                Otp = otp,
        //                Expiry = DateTime.Now.AddMinutes(5),
        //                IsVerified = false
        //            });
        //        }

        //        await _context.SaveChangesAsync();

        //        if (currentCulture == "ar")
        //        {
        //            return Ok(new { success = true, message = "تم إرسال رمز التحقق بنجاح" });
        //        }

        //        else
        //        {
        //            return Ok(new { success = true, message = "OTP sent successfully" });

        //        }

        //        // return Ok(new { success = true, message = "OTP sent successfully" });
        //    }
        //    catch (Exception ex)
        //    {
        //        var currentCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        //        if (currentCulture == "ar")
        //        {
        //            return StatusCode(500, new { message = "فشل إرسال كلمة المرور لمرة واحدة ", error = ex.Message });
        //        }
        //        else
        //        {
        //            return StatusCode(500, new { message = "Failed to send OTP", error = ex.Message });
        //        }

        //        //return StatusCode(500, new { mes sage = "Failed to send OTP", error = ex.Message });
        //    }
        //}
        [HttpPost("forgot-password")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("CustomerAuth")]
        public async Task<IActionResult> SendOtpForPasswordReset([FromBody] OtpRequest request, string lang = "en")
        {
            return await SendCustomerOtp(request.email, true, lang);
        }

        [HttpPost("forgot-password/reset")]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("CustomerAuth")]
        public async Task<IActionResult> VerifyOtpAndResetPassword([FromBody] ResetPasswordRequest request)
        {
            return await ResetCustomerPassword(request.Email, request.Otp, request.NewPassword, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        }




        // -----------------------
        //  Guest Registration
        // -----------------------
        [HttpPost("guest-register")]
        public IActionResult GuestRegister([FromBody] GuestRegisterRequestDto request)
        {
            return LegacyCheckoutUnavailable.Result();
        }
        //public async Task<IActionResult> VerifyOtpAndRegister([FromBody] RegisterRequest request, [FromQuery] string otp)
        //{
        //    //if(!isGuest)
        //    var otpRecord = _context.UserOtps.FirstOrDefault(x => x.Email == request.Email);

        //    if (otpRecord == null || otpRecord.Otp != otp || otpRecord.Expiry < DateTime.Now)
        //        return BadRequest(new { message = "Invalid or expired OTP" });

        //    otpRecord.IsVerified = true;
        //    await _context.SaveChangesAsync();

        //    if (_context.Customers.Any(u => u.Email == request.Email))
        //        return BadRequest(new { message = "Email already exists" });

        //    // ✅ Hash Password (use your existing logic)
        //    string passwordHash = request.Password; // TODO: hash it properly

        //    var newUser = new Customers
        //    {
        //        FullName = request.FullName,
        //        FullNameArb = request.FullNameArb ?? request.FullName,
        //        Email = request.Email,
        //        Phone = request.Phone,
        //        PasswordHash = passwordHash,
        //        RoleId = 2,
        //        IsActive = true,
        //        CreatedAt = DateTime.Now
        //    };

        //    _context.Customers.Add(newUser);
        //    await _context.SaveChangesAsync();

        //    return Ok(new { message = "User registered successfully", userId = newUser.UserId });
        //}

        //public class RegisterRequest
        //{
        //    public string FullName { get; set; }
        //    public string FullNameArb { get; set; }
        //    public string Email { get; set; }
        //    public string Phone { get; set; }
        //    public string Password { get; set; }

        //    //public int? AreaId { get; set; }
        //    //public int? BlockId { get; set; }
        //    //public string Street { get; set; }
        //    //public string HouseNo { get; set; }
        //    //public string Floor { get; set; }
        //    //public string ApartmentNo { get; set; }
        //    //public string Instruction { get; set; }
        //}

        //[HttpPost]
        //public async Task<IActionResult> VerifyOtpAndRegister(
        //        [FromBody] RegisterRequest request,
        //        [FromQuery] string otp,
        //        [FromQuery] bool isGuest = false)
        //{
        //    using var transaction = await _context.Database.BeginTransactionAsync();
        //    try
        //    {
        //        if (!isGuest) // ✅ Normal flow (OTP verification)
        //        {
        //            var otpRecord = _context.UserOtps.FirstOrDefault(x => x.Email == request.Email);

        //            if (otpRecord == null || otpRecord.Otp != otp || otpRecord.Expiry < DateTime.Now)
        //                return BadRequest(new { message = "Invalid or expired OTP" });

        //            otpRecord.IsVerified = true;
        //            await _context.SaveChangesAsync();
        //        }

        //        if (_context.Customers.Any(u => u.Email == request.Email))
        //            return BadRequest(new { message = "Email already exists" });

        //        // ✅ Hash password (add real hashing logic)
        //        string passwordHash = request.Password ?? "Guest@123";

        //        // ----------------------
        //        // 1. Save in Customers
        //        // ----------------------
        //        var newUser = new Customers
        //        {
        //            FullName = request.FullName,
        //            FullNameArb = request.FullNameArb ?? request.FullName,
        //            Email = request.Email,
        //            Phone = request.Phone,
        //            PasswordHash = passwordHash,
        //            RoleId = 2, // Normal user role
        //            IsActive = true,
        //            CreatedAt = DateTime.Now
        //        };

        //        _context.Customers.Add(newUser);
        //        await _context.SaveChangesAsync();

        //        // ----------------------
        //        // 2. Save in UserAddress
        //        // ----------------------
        //        //var area = await _context.Area.FirstOrDefaultAsync(a => a.Id == request.AreaId);
        //        //var block = await _context.Block.FirstOrDefaultAsync(b => b.Id == request.BlockId);

        //        //if (area == null || block == null)
        //        //{
        //        //    return BadRequest(new { message = "Invalid Area or Block" });
        //        //}

        //        //var newAddress = new UserAddress
        //        //{
        //        //    UserId = newUser.UserId,
        //        //    Title = "Home",
        //        //    Area = area.AreaName,         
        //        //    Block = block.BlockName,      
        //        //    Street = request.Street,
        //        //    HouseNo = request.HouseNo,
        //        //    Floor = request.Floor,
        //        //    Landmark = request.Instruction,
        //        //    Phone = request.Phone,
        //        //    IsDefault = true,
        //        //    CreatedAt = DateTime.Now
        //        //};

        //        // ✅ Save new address
        //        //_context.UserAddresses.Add(newAddress);
        //        await _context.SaveChangesAsync();

        //        await transaction.CommitAsync();

        //        return Ok(new
        //        {
        //            message = isGuest
        //                ? "Guest registered successfully"
        //                : "User registered successfully",
        //            userId = newUser.UserId
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        return StatusCode(500, new { message = "Something went wrong", error = ex.Message });
        //    }
        //}




        [HttpGet]
        public IActionResult GetAboutUs(string lang = "en")
        {
            var about = _context.AboutUs
                           .Where(a => a.IsActive == 1)
                           .OrderByDescending(a => a.CreatedAt)
                           .FirstOrDefault();

            if (about == null)
            {
                return NotFound(new { message = "No AboutUs found" });
            }

            var dto = new AboutUsDto
            {
                AboutUsId = about.AboutUsId,
                Title = lang == "ar" ? about.TitleArb : about.TitleEng,
                SubTitle = lang == "ar" ? about.SubTitleArb : about.SubTitleEng,
                Description = lang == "ar" ? about.DescriptionArb : about.DescriptionEng,
                AboutImageUrl = about.AboutImage != null
                    ? "data:image/png;base64," + Convert.ToBase64String(about.AboutImage)
                    : null
            };
            return Ok(dto);
        }


        [HttpGet]
        public IActionResult GetBlogs(string lang = "en")
        {
            var blogs = _context.blogmaster.Where(b => b.IsActive == 1)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BlogDto
                {
                    BlogId = b.BlogId,
                    Title = lang == "ar" ? b.TitleArbic : b.Title,
                    Description = lang == "ar" ? b.DescriptionArb : b.DescriptionEng,
                    BlogImageUrl = b.BlogImage != null
                    ? "data:image/png;base64," + Convert.ToBase64String(b.BlogImage)
                    : null, // ✅ same cover for all
                    Author = "Admin",
                    Tag = "Services",
                    Date = b.CreatedAt.HasValue ? b.CreatedAt.Value.ToString("MMMM dd, yyyy") : "",
                    Link = $"/blog/{b.BlogId}"
                })
                .ToList();

            return Ok(blogs);
        }


        [HttpGet]
        public IActionResult GetBlogById(int blogId, string lang = "en")
        {
            var blog = _context.blogmaster.FirstOrDefault(b => b.BlogId == blogId);
            if (blog == null)
                return NotFound();

            var response = new
            {
                blogId = blog.BlogId,
                title = lang == "ar" ? blog.TitleArbic : blog.Title,
                author = "Admin",
                tag = "Services",
                date = blog.CreatedAt.HasValue ? blog.CreatedAt.Value.ToString("MMMM dd, yyyy") : "",
                blogImageUrl = blog.BlogImage != null
                    ? "data:image/png;base64," + Convert.ToBase64String(blog.BlogImage)
                    : null,
                content = lang == "ar" ? blog.DescriptionArb : blog.DescriptionEng,
            };
            return Ok(response);
        }

        //[HttpPost("sendPayment")]
        //public async Task<IActionResult> SendPayment([FromBody] SendPaymentRequest request)
        //{
        //    if (!ModelState.IsValid)
        //        return BadRequest(ModelState);

        //    if (string.IsNullOrEmpty(request.Language))
        //        request.Language = "EN";

        //    // Callback URLs (for hosted MyFatoorah pages)
        //    request.CallBackUrl ??= Url.Action("VerifyPayment", "PaymentApi", null, Request.Scheme);
        //    request.ErrorUrl ??= Url.Action("PaymentFailed", "PaymentApi", null, Request.Scheme);

        //    var response = await _paymentService.SendPayment(request);

        //    if (response.IsSuccess && response.Data != null)
        //    {
        //        //return Redirect(response.Data.InvoiceURL); // Non-direct payment
        //        // Non-direct payment → redirect URL in response
        //        return Ok(response);
        //    }


        //    return BadRequest(response);
        //}
        //// ----------------- EXECUTE PAYMENT -----------------
        //[HttpPost("executePayment")]
        //public async Task<IActionResult> ExecutePayment([FromBody] ExecutePaymentRequest request)
        //{
        //    if (!ModelState.IsValid)
        //        return BadRequest(ModelState);

        //    if (string.IsNullOrEmpty(request.Language))
        //        request.Language = "EN";

        //    if (!request.IsRecurring)
        //        request.RecurringModel = null;

        //    // Callback URLs
        //    request.CallBackUrl ??= Url.Action("VerifyPayment", "PaymentApi", null, Request.Scheme);
        //    request.ErrorUrl ??= Url.Action("PaymentFailed", "PaymentApi", null, Request.Scheme);

        //    var response = await _paymentService.ExecutePayment(request);

        //    if (response.IsSuccess && response.Data != null)
        //    {
        //        if (!response.Data.IsDirectPayment)
        //            return Redirect(response.Data.PaymentURL); // Non-direct payment return View("ExecutePaymentResult", response); // Direct payment }
        //        return Redirect(response.Data.PaymentURL);
        //    }

        //    return BadRequest(response);
        //}

        // ----------------- VERIFY PAYMENT -----------------
        //[HttpGet("verifyPayment/{paymentId}")]
        //public async Task<IActionResult> VerifyPayment(string paymentId)
        //{
        //    if (string.IsNullOrEmpty(paymentId))
        //        return BadRequest(new { Message = "paymentId is required" });

        //    var request = new GetPaymentStatusRequest
        //    {
        //        KeyType = "PaymentId",
        //        Key = paymentId
        //    };

        //    var response = await _paymentService.GetPaymentStatus(request);

        //    if (response.IsSuccess)
        //        return Ok(response);

        //    return BadRequest(response);
        //}

        //// ----------------- PAYMENT FAILED -----------------
        //[HttpGet("paymentFailed")]
        //public IActionResult PaymentFailed()
        //{
        //    return BadRequest(new { Message = "Payment Failed or Cancelled" });
        //}

        //[HttpGet("verifyPayment/{paymentId}")]
        //public async Task<IActionResult> VerifyPayment(string paymentId)
        //{
        //    if (string.IsNullOrEmpty(paymentId))
        //        return BadRequest(new { Message = "PaymentId is required" });

        //    var request = new GetPaymentStatusRequest
        //    {
        //        KeyType = "PaymentId",
        //        Key = paymentId
        //    };

        //    var response = await _paymentService.GetPaymentStatus(request);

        //    if (response.IsSuccess && response.Data != null)
        //    {
        //        // ✅ Check MyFatoorah payment status
        //        if (response.Data.InvoiceStatus == "Paid")
        //        {


        //            // ✅ Redirect to your frontend success page
        //            return Redirect("https://shopsabahiyacoop.com/payment-success");
        //        }
        //        else
        //        {
        //            // ❌ Redirect to your frontend failure page
        //            return Redirect("https://shopsabahiyacoop.com/payment-failed");
        //        }
        //    }

        //    // ❌ If MyFatoorah API fails
        //    return BadRequest(new
        //    {
        //        Message = "Unable to verify payment.",
        //        Response = response
        //    });
        //}

        //// ----------------- PAYMENT FAILED -----------------
        //[HttpGet("paymentFailed")]
        //public IActionResult PaymentFailed()
        //{
        //    // ✅ Redirect to your frontend failure page instead of just BadRequest
        //    return Redirect("https://shopsabahiyacoop.com/payment-failed");
        //}



        [HttpPost("SucessUpdatedtstatus")]
        public IActionResult UpdateStatus([FromBody] UpdateOrderStatusRequest request)
        {
            return LegacyCheckoutUnavailable.Result();
        }

        [HttpPost("ErrorUpdateStatus")]
        public IActionResult ErrorUpdateStatus([FromBody] UpdateOrderStatusRequest request)
        {
            return LegacyCheckoutUnavailable.Result();
        }
        [HttpGet]
        public IActionResult GetBrandList(string lang = "en")
        {
            try
            {
                var brands = _context.Brand
                    .OrderBy(b => b.BrandNameAEn)
                    .Select(b => new
                    {
                        BrandId = b.BrandId,
                        BrandName = lang == "ar" ? b.BrandNameAr : b.BrandNameAEn,
                        Description = lang == "ar" ? b.Discriptionar : b.Discriptionen,
                        DiscountPercent = b.DiscountPersent,

                        BrandImageUrl = b.BrandLogo != null
                            ? "data:image/png;base64," + Convert.ToBase64String(b.BrandLogo)
                            : null,

                        CreatedAt = b.CreatedAt != null
                            ? b.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                            : null
                    })
                    .ToList();

                return Ok(brands);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Something went wrong while fetching brands.",
                    error = ex.InnerException?.Message ?? ex.Message
                });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetProductsByBrand(int brandId)
        {
            try
            {
                var products = await _context.Products
                    .Where(p => p.BrandId == brandId)
                    .Select(p => new
                    {
                        p.ProductId,
                        p.CategoryId,
                        p.BrandId,
                        p.SubCategoryId,
                        p.Ds_Barcode,
                        p.Ds_ProdID,
                        p.Name,
                        p.NameArb,
                        p.Description,
                        p.DescriptionArb,
                        p.Price,
                        p.CoastPrice,
                        p.Stock,
                        p.Weight,
                        p.SKUId,
                        p.ProductsModel,
                        p.Warranty,
                        p.Units,
                        p.CreatedAt,
                        p.IsActive,
                        p.CreatedBy,

                        ImageBase64 = p.ProductImages
                            .Select(img => "data:image/png;base64," + Convert.ToBase64String(img.ImageData))
                            .FirstOrDefault() ?? "/assets/img/product/placeholder.png"
                    })
                    .ToListAsync();

                if (products == null || !products.Any())
                    return NotFound(new { success = false, message = "No products found for this brand" });

                return Ok(new
                {
                    success = true,
                    total = products.Count,
                    data = products
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public IActionResult ClearCart(int? userId, string? deviceId, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor || actor != userId) return Unauthorized();
            var count = _context.CartItems.Where(c => c.UserId == actor && c.VendorListingId == null).ExecuteDelete();
            return Ok(new
            {
                success = true,
                message = lang?.ToLower() == "ar"
                    ? (count == 0 ? "السلة فارغة بالفعل" : "تم مسح محتويات السلة بنجاح")
                    : (count == 0 ? "Cart already empty" : "Cart cleared successfully")
            });
        }



        public async Task<IActionResult> GetAreas()
        {
            var areas = await _context.Area
                .Select(a => new { a.Id, a.AreaName, a.AreaNameArb })
                .OrderBy(a => a.AreaName)
                .ToListAsync();

            return Ok(areas);
        }


        public async Task<IActionResult> GetBlocks(int areaId)
        {
            var blocks = await _context.Block
                .Where(b => b.AreaId == areaId)
                .Select(b => new { b.Id, b.BlockName, b.BlockNameArb })
                .OrderBy(b => b.BlockName)
                .ToListAsync();

            return Ok(blocks);
        }

        public class CheckoutBasicInfoDto
        {
            public decimal VatPercentage { get; set; }
            public decimal DeliveryCharge { get; set; }

            public List<UserAddress> AddressList { get; set; } = new();
        }
        [HttpGet]
        public async Task<IActionResult> GetCheckoutBasicInfo(int? userid)
        {
            if (userid.HasValue && (await OptionalCustomerIdAsync() is not int actor || actor != userid.Value))
                return Unauthorized();
            var result = new CheckoutBasicInfoDto();


            var company = _context.CompanyDetails
                .Select(x => new
                {
                    x.Vat,
                    x.DeliveryCharge
                })
                .FirstOrDefault();

            if (company != null)
            {
                result.VatPercentage = company.Vat;
                result.DeliveryCharge = company.DeliveryCharge;
            }

            if (userid.HasValue)
            {
                result.AddressList = (from ua in _context.UserAddresses
                                      join a in _context.Area
                                          on ua.Area equals a.Id.ToString() into areaGroup
                                      from a in areaGroup.DefaultIfEmpty()

                                      join b in _context.Block
                                          on ua.Block equals b.Id.ToString() into blockGroup
                                      from b in blockGroup.DefaultIfEmpty()


                                      where ua.UserId == userid.Value && ua.isdeleted == 0
                                      orderby ua.IsDefault descending, ua.AddressId

                                      select new UserAddress
                                      {
                                          HouseNo = ua.HouseNo,
                                          AddressId = ua.AddressId,
                                          Title = ua.Title,
                                          Phone = ua.Phone,
                                          Area = a.AreaName,
                                          Block = b.BlockName,
                                          IsDefault = ua.IsDefault
                                      }).ToList();
            }

            return Ok(result);
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> ApplyCoupon([FromBody] ApplyCouponRequest request, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor || request == null || actor != request.UserId) return Unauthorized();
            try
            {
                var currentCulture = lang?.ToLower();
                var userId = request.UserId.ToString();

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = currentCulture == "ar"
                            ? "المستخدم غير مسجل الدخول"
                            : "User not logged in"
                    });
                }

                var coupon = await _context.Coupons
                    .Where(c => c.Code == request.Code)
                    .FirstOrDefaultAsync();


                if (coupon == null || !coupon.IsActive)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = currentCulture == "ar"
                            ? "قسيمة غير صالحة أو غير نشطة"
                            : "Invalid or inactive coupon"
                    });
                }

                var now = DateTime.Now;


                if (coupon.ValidFrom.HasValue && coupon.ValidFrom.Value > now)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = currentCulture == "ar"
                            ? "القسيمة غير صالحة بعد"
                            : "Coupon not yet valid"
                    });
                }

                if (coupon.ValidTo.HasValue && coupon.ValidTo.Value < now)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = currentCulture == "ar"
                            ? "انتهت صلاحية القسيمة"
                            : "Coupon expired"
                    });
                }


                if (coupon.MinOrderAmount.HasValue && request.OrderAmount < coupon.MinOrderAmount.Value)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = currentCulture == "ar"
                            ? $"الحد الأدنى لمبلغ الطلب هو {coupon.MinOrderAmount.Value}"
                            : $"Minimum order amount is {coupon.MinOrderAmount.Value}"
                    });
                }


                var used = await _context.CouponUsage
                    .AnyAsync(x => x.UserId.ToString() == userId && x.CouponId == coupon.CouponId);

                if (used)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = currentCulture == "ar"
                            ? "لقد استخدمت هذا القسيمة مسبقًا!"
                            : "You already used this coupon!"
                    });
                }


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
        public IActionResult PlaceOrder([FromBody] PlaceOrderDto request, string lang = "en")
        {
            return LegacyCheckoutUnavailable.Result();
        }


        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public IActionResult AddProductReview([FromBody] ReviewDto dto, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor || actor != dto.UserId) return Unauthorized();
            var currentCulture = lang?.ToLower();

            if (dto.Rating < 1 || dto.Rating > 5)
            {
                return BadRequest(new
                {
                    message = currentCulture == "ar"
                        ? "يجب أن يكون التقييم بين 1 و 5"
                        : "Rating must be between 1 and 5"
                });
            }

            var userExists = _context.Customers.Any(u => u.UserId == dto.UserId && u.IsDeleted == 0);
            if (!userExists)
            {
                return BadRequest(new
                {
                    message = currentCulture == "ar"
                        ? "مستخدم غير صالح. الرجاء تسجيل الدخول مرة أخرى."
                        : "Invalid user. Please login again."
                });
            }

            var productExists = _context.Products.Any(p => p.ProductId == dto.ProductId);
            if (!productExists)
            {
                return BadRequest(new
                {
                    message = currentCulture == "ar"
                        ? "المنتج غير موجود"
                        : "Product not found"
                });
            }

            var review = new ProductReview
            {
                ProductId = dto.ProductId,
                UserId = dto.UserId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.Now
            };

            _context.ProductReviews.Add(review);
            _context.SaveChanges();

            return Ok(new
            {
                message = currentCulture == "ar"
                    ? "تم إرسال التقييم بنجاح"
                    : "Review submitted successfully"
            });
        }

        //[HttpPost]
        //public async Task<IActionResult> CancelOrder(int orderId)
        //{
        //    var currentCulture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        //    var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == actor);

        //    if (order == null)
        //    {
        //        if (currentCulture == "ar")
        //            return NotFound("الطلب غير موجود");
        //        else
        //            return NotFound("Order not found");
        //    }

        //    if (order.Status == 2 || order.Status == 4 || order.Status == 5)
        //    {
        //        if (currentCulture == "ar")
        //            return BadRequest("❌ لا يمكن الإلغاء بعد الشحن");
        //        else
        //            return BadRequest(new { message = "❌ Cannot cancel after shipment" });
        //    }
        //    if (order.Status == 3)
        //    {
        //        return BadRequest(new { message = "Already Cancelled Order" });
        //    }


        //    order.Status = 3;
        //    order.UpdatedAt = DateTime.Now;
        //    await _context.SaveChangesAsync();

        //    if (currentCulture == "ar")
        //    {
        //        return Ok(new { message = "تم إلغاء الطلب" });
        //    }
        //    else
        //        return Ok(new { message = "Order cancelled" });
        //}
        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public async Task<IActionResult> CancelOrder(int orderId, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor) return Unauthorized();
            bool isArabic = lang?.ToLower() == "ar";

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var order = await _context.Orders.FindAsync(orderId);

                if (order == null)
                {
                    return NotFound(isArabic
                        ? "الطلب غير موجود"
                        : "Order not found");
                }


                if (order.Status == 2 || order.Status == 4 || order.Status == 5)
                {
                    return BadRequest(new
                    {
                        message = isArabic
                            ? "لا يمكن الإلغاء بعد الشحن"
                            : "Cannot cancel after shipment"
                    });
                }

                // ❌ Already cancelled
                if (order.Status == 3)
                {
                    return BadRequest(new
                    {
                        message = isArabic
                            ? "تم إلغاء الطلب بالفعل"
                            : "Order already cancelled"
                    });
                }


                var orderItems = await _context.OrderItems
                    .Where(x => x.OrderId == orderId)
                    .ToListAsync();


                foreach (var item in orderItems)
                {
                    _context.ProductStocks.Add(new ProductStock
                    {
                        ProductId = item.ProductId,
                        StockQty = item.Quantity,
                        CreatedAt = DateTime.Now,
                        CreatedBy = 1
                    });
                }


                order.Status = 3; // Cancelled
                order.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = isArabic
                        ? "تم إلغاء الطلب وإرجاع المخزون"
                        : "Order cancelled and stock restored"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    message = isArabic ? "فشل إلغاء الطلب" : "Cancel failed",
                    error = ex.Message
                });
            }
        }



        [HttpPost]
        public async Task<IActionResult> SubmitMessage([FromBody] ContactMessage message, string lang = "en")
        {
            var currentCulture = lang?.ToLower();
            bool isArabic = currentCulture == "ar";

            if (string.IsNullOrWhiteSpace(message.FullName) ||
                string.IsNullOrWhiteSpace(message.Email) ||
                string.IsNullOrWhiteSpace(message.Message))
            {
                return BadRequest(new
                {
                    success = false,
                    message = isArabic ? "جميع الحقول مطلوبة" : "Fields are required"
                });
            }

            message.SubmittedAt = DateTime.Now;
            _context.ContactMessages.Add(message);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = isArabic ? "تم إرسال الرسالة بنجاح." : "Message submitted successfully."
            });
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public IActionResult SoftDeleteUser(int userId, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor || actor != userId) return Unauthorized();
            var currentCulture = lang?.ToLower();
            var user = _context.Customers.FirstOrDefault(u => u.UserId == userId && u.IsDeleted == 0);

            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = lang?.ToLower() == "ar" ? "المستخدم غير موجود." : "User not found."
                });
            }

            if (user.IsDeleted == 1)
            {
                return BadRequest(new
                {
                    success = false,
                    message = lang?.ToLower() == "ar" ? "المستخدم غير موجود." : "User not found."
                });
            }


            user.IsDeleted = 1;
            user.PasswordHash = "0";
            _context.Customers.Update(user);
            _context.SaveChanges();


            return Ok(new
            {
                success = true,
                message = lang?.ToLower() == "ar" ? "تم حذف المستخدم بنجاح." : "User deleted successfully."
            });

        }
        [HttpGet]
        [Authorize(AuthenticationSchemes = CustomerApiAuth.Scheme, Roles = "Customer")]
        public IActionResult GetOrderDetails(int orderid, string lang = "en")
        {
            if (CurrentCustomerId() is not int actor) return Unauthorized();
            try
            {
                bool isArabic = lang?.ToLower() == "ar";

                var order = (from o in _context.Orders

                             join u in _context.Customers on o.CustomerId equals u.UserId
                             join st in _context.OrderStatus on o.Status equals st.OrderstatusId
                             join pyt in _context.PaymentStatus on o.PaymentStatus equals pyt.PaymentstatusId


                             join ad in _context.UserAddresses
                                 on o.DeliveryAddress equals ad.AddressId into addrGroup
                             from ad in addrGroup.DefaultIfEmpty()

                             join ar in _context.Area on ad.Area equals ar.Id.ToString() into areaGroup
                             from ar in areaGroup.DefaultIfEmpty()

                             join b in _context.Block on ad.Block equals b.Id.ToString() into blockGroup
                             from b in blockGroup.DefaultIfEmpty()

                             where o.OrderId == orderid && o.CustomerId == actor

                             select new
                             {
                                 OrderId = o.OrderId,
                                 UserId = o.CustomerId,
                                 ShareholderNo = u.ShareholderNo,

                                 Status = isArabic ? st.OrderStatusNameArb : st.OrderstatusName,
                                 PaymentStatus = isArabic ? pyt.PaymentStatusNameArb : pyt.PaymentstatusName,
                                 PaymentType = o.PaymentType.ToString(),
                                 CouponCode = o.CouponCode,
                                 DiscountAmount = o.DiscountAmount,
                                 CreatedAt = o.CreatedAt,

                                 FullName = isArabic ? u.FullNameArb : u.FullName,
                                 Email = u.Email,
                                 Phone = u.Phone,

                                 // Items total
                                 Price = _context.OrderItems
                                            .Where(x => x.OrderId == o.OrderId)
                                            .Sum(x => (decimal?)(x.Price ?? 0) * (x.Quantity ?? 0)) ?? 0,

                                 Vatpersent = o.VatPercent,
                                 VatAmount = o.VatAmount,
                                 DeliveryCharge = o.DeliveryCharge,

                                 // Address
                                 House = ad != null ? ad.HouseNo : "",
                                 Street = ad != null ? ad.Street : "",
                                 Landmark = ad != null ? ad.Landmark : "",
                                 Floor = ad != null ? ad.Floor : "",
                                 Governorate = ad != null ? ad.Governorate : "",

                                 Block = b != null
                                     ? (isArabic ? b.BlockNameArb : b.BlockName)
                                     : (ad != null ? ad.Block : ""),

                                 Area = ar != null
                                     ? (isArabic ? ar.AreaNameArb : ar.AreaName)
                                     : (ad != null ? ad.Area : ""),

                                 // Items list
                                 Items = (from ot in _context.OrderItems
                                          join p in _context.Products on ot.ProductId equals p.ProductId
                                          where ot.OrderId == o.OrderId
                                          select new
                                          {
                                              ProductName = isArabic ? p.NameArb : p.Name,
                                              Quantity = ot.Quantity ?? 0,
                                              Price = ot.Price ?? 0,

                                              ProductImageBase64 = p.ProductImages
                                          .OrderBy(img => img.ImageId)
                                          .Select(img => "data:" + img.ContentType + ";base64," +
                                                         Convert.ToBase64String(img.ImageData))
                                          .FirstOrDefault()
                                          }).ToList()
                             }).FirstOrDefault();

                if (order == null)
                    return NotFound(new { success = false, message = "Order not found" });

                return Ok(new { success = true, data = order });
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

    }

}




