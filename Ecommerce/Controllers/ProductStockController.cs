using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace EcommerceWebApplication.Controllers.Admin
{
    [Authorize(Roles = AdminStaffAccess.Roles)]
    public class ProductStockController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly AdminStaffAccess _access;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private bool MarketplaceInventoryEnabled => _environment.IsDevelopment() &&
            _configuration.GetValue("Marketplace:VendorMasterEnabled", false) &&
            _configuration.GetValue("Marketplace:VendorInventoryEnabled", false);

        public ProductStockController(EcommerceDbContext context, AdminStaffAccess access, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _context = context;
            _access = access;
            _configuration = configuration;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string name, string status, int page = 1, int pageSize = 10, bool partial = false)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Stock, HttpContext.RequestAborted)).View) return Forbid();
            ViewBag.MarketplaceInventoryEnabled = MarketplaceInventoryEnabled;
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            var products = (from p in _context.Products
                            join s in _context.ProductStocks
                                on p.ProductId equals s.ProductId into stockGroup
                            select new ProductStockViewModel
                            {
                                ProductId = p.ProductId,
                               
                                ProductName = isArabic ? p.NameArb : p.Name,
                                TotalStock = stockGroup.Sum(x => (int?)x.StockQty) ?? 0,
                                Totalsoldstock = _context.OrderItems
                                                .Where(o => o.ProductId == p.ProductId && o.VendorOrderId == null)
                                                .Sum(o => (int?)o.Quantity) ?? 0,
                                RemainingQty = (stockGroup.Sum(x => (int?)x.StockQty) ?? 0) -
                                               (_context.OrderItems.Where(o => o.ProductId == p.ProductId && o.VendorOrderId == null)
                                               .Sum(o => (int?)o.Quantity) ?? 0),
                             
                                Status = ((stockGroup.Sum(x => (int?)x.StockQty) ?? 0) -
                                        (_context.OrderItems
                                        .Where(o => o.ProductId == p.ProductId && o.VendorOrderId == null)
                                        .Sum(o => (int?)o.Quantity) ?? 0)) > 10
                                        ? "In"
                                        : ((stockGroup.Sum(x => (int?)x.StockQty) ?? 0) -
                                        (_context.OrderItems
                                       .Where(o => o.ProductId == p.ProductId && o.VendorOrderId == null)
                                       .Sum(o => (int?)o.Quantity) ?? 0)) > 0
                                        ? "Low"
                                        : "Out"
                            });
            if (!string.IsNullOrEmpty(name))
            {
                products = products
                    .Where(p => p.ProductName.Contains(name))
                    .OrderBy(p => p.ProductName).ThenBy(p => p.ProductId);
            }
            else
            {
                products = products.OrderByDescending(p => p.ProductId);
            }

           
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "In") 
                {
                    products = products.Where(p => p.RemainingQty > 10);
                }
                else if (status == "Low") 
                {
                    products = products.Where(p => p.RemainingQty > 0 && p.RemainingQty <= 10);
                }
                else if (status == "Out") 
                {
                    products = products.Where(p => p.RemainingQty <= 0);
                }
            }
            pageSize = Math.Clamp(pageSize, 1, 100);
            int totalItems = products.Count();
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize)));
            var pagedData = products
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedModel = new PagedResult<ProductStockViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            ViewBag.SearchName = name;
            ViewBag.FilterStatus = status;

            return partial ? PartialView("_RegisterRows", pagedModel) : View(pagedModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult AddStock(int productId, int newStock)
        {
            if (!int.TryParse(User.FindFirst("UserId")?.Value, out var userId) || !_context.Users.Any(u => u.UserId == userId && u.IsActive))
                return Json(new { success = false, message = "Administrator session is invalid." });
            if (!ModelState.IsValid || newStock <= 0 || !_context.Products.Any(p => p.ProductId == productId))
                return Json(new { success = false, message = "Invalid product or stock quantity." });

            using var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var marketplaceSchemaExists = _context.Database.SqlQuery<int>($"""
                SELECT CASE WHEN OBJECT_ID(N'dbo.VendorListings',N'U') IS NULL THEN 0 ELSE 1 END AS Value
                """).Single() == 1;
            if (marketplaceSchemaExists && _context.VendorListings.Any(l => l.ProductId == productId))
                return Json(new { success = false, message = "Use marketplace stock for listed products." });
            var stock = new ProductStock
            {
                ProductId = productId,
                StockQty = newStock,
                CreatedAt = DateTime.Now,
                CreatedBy = userId
            };

            _context.ProductStocks.Add(stock);
            _context.SaveChanges();
            transaction.Commit();

            return Json(new { success = true, message = "Stock added successfully!" });
        }
        [HttpGet]
        public async Task<IActionResult> MarketplaceStock(int? vendorId, string? search, int page = 1, CancellationToken ct = default)
        {
            if (!MarketplaceInventoryEnabled) return NotFound();
            var rights = await _access.RightsAsync(User, AdminStaffAccess.Stock, ct);
            if (!rights.View) return Forbid();
            page = Math.Clamp(page, 1, 100000);
            search = search?.Trim() ?? "";
            if (search.Length > 100) return BadRequest();
            var balances = _context.Database.SqlQuery<MarketplaceStockRow>($"""
                SELECT l.VendorListingId AS ListingId,l.VendorId,v.Name AS VendorName,
                    p.Name AS ProductName,p.NameArb AS ProductNameArb,l.SellerSku,l.Status AS ListingStatus,
                    COALESCE(b.OnHandQuantity,CONVERT(bigint,0)) AS OnHand,
                    COALESCE(b.ReservedQuantity,CONVERT(bigint,0)) AS Reserved,
                    COALESCE(b.AvailableQuantity,CONVERT(bigint,0)) AS Available
                FROM dbo.VendorListings l JOIN dbo.Vendors v ON v.VendorId=l.VendorId
                JOIN dbo.Products p ON p.ProductId=l.ProductId
                LEFT JOIN dbo.InventoryBalances b ON b.VendorListingId=l.VendorListingId
                """);
            if (vendorId.HasValue) balances = balances.Where(x => x.VendorId == vendorId);
            if (search.Length > 0) balances = balances.Where(x => x.ProductName.Contains(search) ||
                (x.ProductNameArb != null && x.ProductNameArb.Contains(search)) || x.SellerSku.Contains(search) || x.VendorName.Contains(search));
            var rows = await balances.OrderBy(x => x.VendorId).ThenBy(x => x.ListingId)
                .Skip((page - 1) * 25).Take(26).ToListAsync(ct);
            return View(new MarketplaceStockRegister { VendorId = vendorId, Search = search, Page = page,
                CanEdit = rights.Edit, HasNext = rows.Count > 25, Rows = rows.Take(25).ToList() });
        }

        [HttpGet]
        public async Task<IActionResult> MarketplaceMovements(int id, int listingId, int page = 1, CancellationToken ct = default)
        {
            if (!MarketplaceInventoryEnabled) return NotFound();
            var rights = await _access.RightsAsync(User, AdminStaffAccess.Stock, ct);
            if (!rights.View) return Forbid();
            var result = await ListingStockQueries.ReadAsync(_context,id,listingId,page,rights.Edit,ct);
            if (result == null) return NotFound();
            ViewData["AdminStock"] = true;
            return View("~/Views/VendorPortal/Stock.cshtml", result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarketplaceMovements(int id, int listingId, StockAdjustmentForm adjustment, CancellationToken ct)
        {
            if (!MarketplaceInventoryEnabled) return NotFound();
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Stock, ct)).Edit ||
                !int.TryParse(User.FindFirst("UserId")?.Value,out var actor)) return Forbid();
            if (!ModelState.IsValid || adjustment.RequestId == Guid.Empty || adjustment.Quantity is < 1 or > 1000000000 ||
                adjustment.Direction is not ("Add" or "Reduce") || string.IsNullOrWhiteSpace(adjustment.Reason))
                return BadRequest();
            var delta = adjustment.Direction == "Add" ? adjustment.Quantity : -adjustment.Quantity;
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    EXEC dbo.PostInventoryAdjustment @VendorId={id}, @VendorListingId={listingId}, @UserId={actor},
                        @RequestId={adjustment.RequestId}, @Delta={delta}, @Reason={adjustment.Reason.Trim()}, @AdminPortal=1
                    """,ct);
            }
            catch (SqlException ex) when (ex.Number == 51312) { return Forbid(); }
            catch (SqlException ex) when (ex.Number is 51310 or 51313 or 51314)
            {
                TempData["StockError"] = "Stock not changed. Review quantity and retry. / لم يتغير المخزون. راجع الكمية.";
                return RedirectToAction(nameof(MarketplaceMovements),new { id,listingId });
            }
            TempData["StockSuccess"] = "Stock updated. / تم تحديث المخزون.";
            return RedirectToAction(nameof(MarketplaceMovements),new { id,listingId });
        }

        [HttpGet]
        public async Task<IActionResult> AutoCompleteSql(string term)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Stock, HttpContext.RequestAborted)).View) return Forbid();
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
