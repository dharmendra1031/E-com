using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceWebApplication.Controllers
{
    public class WebController : Controller
    {
        // GET: WebController
        public ActionResult Index()
        {
            return View();
        }

        // GET: WebController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: WebController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: WebController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: WebController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: WebController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: WebController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: WebController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
