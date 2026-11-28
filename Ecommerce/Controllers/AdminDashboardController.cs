using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
  
    [Authorize(Roles = AdminStaffAccess.Roles)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]

    public class AdminDashboardController : Controller
    {  
        private readonly EcommerceDbContext _context;
        private readonly AdminStaffAccess _access;
        string culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        public AdminDashboardController(EcommerceDbContext context, AdminStaffAccess access)
        {
            _context = context;
            _access = access;
        }
       
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Dashboard, HttpContext.RequestAborted)).View) return Forbid();
            if (AdminStaffAccess.IsRestricted(User))
            {
                var ct=HttpContext.RequestAborted;
                var masters=new Dictionary<string,int>();
                if((await _access.RightsAsync(User,AdminStaffAccess.Customers,ct)).View) masters["Customers"]=await _context.Customers.CountAsync(u=>u.RoleId==2 && u.IsDeleted==0,ct);
                if((await _access.RightsAsync(User,AdminStaffAccess.Products,ct)).View) masters["Products"]=await _context.Products.CountAsync(ct);
                if((await _access.RightsAsync(User,AdminStaffAccess.Categories,ct)).View) masters["Categories"]=await _context.Categories.CountAsync(ct);
                if((await _access.RightsAsync(User,AdminStaffAccess.Subcategories,ct)).View) masters["Sub-Categories"]=await _context.SubCategories.CountAsync(ct);
                if((await _access.RightsAsync(User,AdminStaffAccess.Areas,ct)).View) masters["Areas"]=await _context.Area.CountAsync(ct);
                return View("RestrictedOverview",masters);
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
