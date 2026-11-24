using EcommerceWebApplication.Data;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceWebApplication.Controllers
{
    public class WebBlogController : Controller
    {
        private readonly EcommerceDbContext _context;

        public WebBlogController(EcommerceDbContext context)
        {
            _context = context;
        }
        public IActionResult WebBlog()
        {
            var blogs = _context.blogmaster.Where(b => b.IsActive == 1).ToList();
            return View(blogs);
        }
    }
}
