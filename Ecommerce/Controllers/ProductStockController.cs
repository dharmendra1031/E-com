using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers.Admin
{
    [Authorize]
    public class ProductStockController : Controller
    {
        private readonly EcommerceDbContext _context;

        public ProductStockController(EcommerceDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(string name, string status, int page = 1, int pageSize = 10)
        {
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
                                                .Where(o => o.ProductId == p.ProductId)
                                                .Sum(o => (int?)o.Quantity) ?? 0,
                                RemainingQty = (stockGroup.Sum(x => (int?)x.StockQty) ?? 0) -
                                               (_context.OrderItems.Where(o => o.ProductId == p.ProductId)
                                               .Sum(o => (int?)o.Quantity) ?? 0),
                             
                                Status = ((stockGroup.Sum(x => (int?)x.StockQty) ?? 0) -
                                        (_context.OrderItems
                                        .Where(o => o.ProductId == p.ProductId)
                                        .Sum(o => (int?)o.Quantity) ?? 0)) > 10
                                        ? "In"
                                        : ((stockGroup.Sum(x => (int?)x.StockQty) ?? 0) -
                                        (_context.OrderItems
                                       .Where(o => o.ProductId == p.ProductId)
                                       .Sum(o => (int?)o.Quantity) ?? 0)) > 0
                                        ? "Low"
                                        : "Out"
                            });
            if (!string.IsNullOrEmpty(name))
            {
                products = products
                    .Where(p => p.ProductName.Contains(name))
                    .OrderBy(p => p.ProductName);
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
                    products = products.Where(p => p.RemainingQty == 0);
                }
            }
            // 🔹 Convert to list
            var productList = products.ToList();

            // 🔹 Pagination
            int totalItems = productList.Count;
            var pagedData = productList
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

            return View(pagedModel);
        }

        [HttpPost]
        public IActionResult AddStock(int productId, int newStock)
        {
            var stock = new ProductStock
            {
                ProductId = productId,
                StockQty = newStock,
                CreatedAt = DateTime.Now,
                CreatedBy = 1 // 🔹 Replace with logged-in user id
            };

            _context.ProductStocks.Add(stock);
            _context.SaveChanges();

            return Json(new { success = true, message = "Stock added successfully!" });
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
