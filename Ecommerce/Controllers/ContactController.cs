using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = AdminStaffAccess.Roles)]
    public class ContactController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly AdminStaffAccess _access;

        public ContactController(EcommerceDbContext context, AdminStaffAccess access)
        {
            _context = context;
            _access = access;
        }
   
        [HttpGet]
        public async Task<IActionResult> ContactList(string name, int page = 1, int pageSize = 10)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Contact, HttpContext.RequestAborted)).View) return Forbid();
            var query = _context.ContactMessages.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(c => c.FullName.Contains(name));
            }

            pageSize=Math.Clamp(pageSize,1,100);
            int totalItems=await query.CountAsync();
            page=Math.Clamp(page,1,Math.Max(1,(int)Math.Ceiling((double)totalItems/pageSize)));
            ViewBag.SearchName=name;
            var pagedData=await query.AsNoTracking().OrderByDescending(c=>c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagedModel = new PagedResult<ContactMessage>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
            return View(pagedModel);
        }
        [HttpGet]
        public async Task<IActionResult> AutoCompleteSql(string term)
        {
            if (!(await _access.RightsAsync(User, AdminStaffAccess.Contact, HttpContext.RequestAborted)).View) return Forbid();
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            IQueryable<ContactMessage> queryable = _context.ContactMessages;

            // Detect if input is Arabic
            bool isArabic = term.Any(c => c >= 0x0600 && c <= 0x06FF);

          
                queryable = queryable.Where(c => c.FullName.Contains(term));
        
            var suggestions = await queryable
                .Select(c => c.FullName)
                .Take(10)
                .ToListAsync();
            return Json(suggestions);
        }
    }
}
