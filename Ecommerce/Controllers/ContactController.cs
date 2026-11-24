using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
    [Authorize]
    public class ContactController : Controller
    {
        private readonly EcommerceDbContext _context;

        public ContactController(EcommerceDbContext context)
        {
            _context = context;
        }
   
        public IActionResult ContactList(string name, int page = 1, int pageSize = 10)
        {
            var query = _context.ContactMessages.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(c => c.FullName.Contains(name));
            }

            var contacts = query
                            .OrderByDescending(c => c.Id)
                            .AsNoTracking()
                            .ToList();

            ViewBag.SearchName = name; // To retain input in view
            int totalItems = contacts.Count;
            var pagedData = contacts
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

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
        public async Task<JsonResult> AutoCompleteSql(string term)
        {
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
