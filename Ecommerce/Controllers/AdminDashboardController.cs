using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
  
    [Authorize(Roles = "Admin")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]

    public class AdminDashboardController : Controller
    {  
        private readonly EcommerceDbContext _context;
        string culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        public AdminDashboardController(EcommerceDbContext context)
        {
            _context = context;
        }
       
        public IActionResult Index()
        {
            // ✅ Double-check role claim
            if (!User.IsInRole("Admin"))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            ViewBag.TodayOrders = _context.Orders.Count(o => o.CreatedAt >= today && o.CreatedAt < tomorrow);
            ViewBag.TodayPayments = _context.Payments.Where(p => p.PaymentStatus == 1 && p.PaidAt >= today && p.PaidAt < tomorrow).Sum(p => (decimal?)p.AmountPaid) ?? 0m;
            ViewBag.customerCount = _context.Customers.Count(u => u.RoleId == 2 && u.IsDeleted == 0);
            ViewBag.categoryCount = _context.Categories.Count();
            ViewBag.SubCategoriesCount = _context.SubCategories.Count();
            ViewBag.ProductsCount = _context.Products.Count();
            ViewBag.PendingOrders = _context.Orders.Where(o => o.Status == 1).Count();
            ViewBag.AcceptedOrders = _context.Orders.Where(o => o.Status == 2).Count();
            ViewBag.CompletedOrders = _context.Orders.Where(o => o.Status == 5).Count();
            ViewBag.CancelledOrders = _context.Orders.Where(o => o.Status == 4).Count();
            ViewBag.TotalArea = _context.Area.Count();
            ViewBag.TotalSales = _context.Payments.Where(p => p.PaymentStatus == 1).Sum(p => (decimal?)p.AmountPaid);
            ViewBag.LowStock = _context.Products.Where(p => p.IsActive)
                .Count(p => (_context.ProductStocks.Where(st => st.ProductId == p.ProductId).Sum(st => (int?)st.StockQty) ?? 0)
                    - (_context.OrderItems.Where(o => o.ProductId == p.ProductId).Sum(o => (int?)o.Quantity) ?? 0) <= 10);
            var data = (from o in _context.Orders
                        join u in _context.Customers on o.CustomerId equals u.UserId
                       
                        join st in _context.OrderStatus on o.Status equals st.OrderstatusId
                        orderby o.OrderId descending   // Order by OrderId descending
                        select new OrderItemViewModel
                        {
                            OrderId = o.OrderId,
                            FullName = u.FullName,
                            Paytype = o.PaymentType,
                            CreatedAt = o.CreatedAt,
                            OrderStatus = (culture == "ar") ? st.OrderStatusNameArb : st.OrderstatusName,
                            TotalPrice = o.TotalAmount
                        }).Take(10).ToList();

            ViewBag.OrderItems = data;


          

            return View();
        }
    }
}
