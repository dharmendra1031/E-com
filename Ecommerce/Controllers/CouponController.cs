using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CouponController : Controller
    {

        private readonly EcommerceDbContext _context;

        public CouponController(EcommerceDbContext context)
        {
            _context = context;
        }

        //public ActionResult Index(int page = 1, int pageSize = 10)
        //{
        //    var query = _context.Coupons.Select(c => new CouponViewModel
        //    {
        //        CouponId = c.CouponId,
        //        Code = c.Code,
        //        DiscountType = c.DiscountType,
        //        DiscountValue = c.DiscountValue, // 🔧 Include this if needed in View
        //        MinOrderAmount = c.MinOrderAmount,
        //        MaxDiscountAmount = c.MaxDiscountAmount,
        //        ValidFrom =  c.ValidFrom,
        //        ValidTo = c.ValidTo,
        //        IsActive = c.IsActive
        //    });

        //    var totalRecords = query.Count();

        //    var items = query
        //        .Skip((page - 1) * pageSize)
        //        .Take(pageSize)
        //        .ToList();

        //    var result = new PagedResult<CouponViewModel>
        //    {
        //        Items = items,
        //        PageNumber = page,
        //        PageSize = pageSize,
        //        TotalItems = totalRecords
        //        // = (int)Math.Ceiling((double)totalRecords / pageSize)
        //    };

        //    return View(result);
        //}
        public ActionResult Index(string discountType, bool? isActive, int page = 1, int pageSize = 10)
        {
            var coupons = _context.Coupons.AsQueryable();

            bool isFilterApplied = !string.IsNullOrEmpty(discountType) || isActive.HasValue;

          
            if (!string.IsNullOrEmpty(discountType))
            {
                coupons = coupons.Where(c => c.DiscountType == discountType);
            }

         
            if (isActive.HasValue)
            {
                coupons = coupons.Where(c => c.IsActive == isActive.Value);
            }

           
            coupons = isFilterApplied
                ? coupons.OrderBy(c => c.CouponId)        
                : coupons.OrderByDescending(c => c.CouponId); 

            var couponList = coupons.Select(c => new CouponViewModel
            {
                CouponId = c.CouponId,
                Code = c.Code,
                DiscountType = c.DiscountType,
                DiscountValue = c.DiscountValue,
                MinOrderAmount = c.MinOrderAmount,
                NoOfUsers = c.NoOfUsers,
                IsActive = c.IsActive,
                NoOfUses = _context.Orders.Count(o => o.CouponCode == c.Code)
            }).ToList();

            // 🔹 Pagination
            int totalItems = couponList.Count;
            var pagedData = couponList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedModel = new PagedResult<CouponViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
            ViewBag.DiscountTypeFilter = discountType;
            ViewBag.IsActiveFilter = isActive;

           

            return View(pagedModel);
        }
        public ActionResult Create(int? id)
        {
            if (id == null)
            {
                return View();
            }
            else
            {
                var model = _context.Coupons.Find(id);
                if (model == null)
                {
                    return NotFound();
                }

                // Date ko string me convert karke NotMapped property me set karo
                model.ValidFromm = Convert.ToDateTime(model.ValidFrom).ToString("dd-MM-yyyy");
                model.ValidToo = Convert.ToDateTime(model.ValidTo).ToString("dd-MM-yyyy");

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Coupon model)
        {
            try
            {
                
                if (!string.IsNullOrEmpty(model.ValidFromm))
                {
                    model.ValidFrom = DateTime.ParseExact(model.ValidFromm, "dd-MM-yyyy", null);
                }

                if (!string.IsNullOrEmpty(model.ValidToo))
                {
                    model.ValidTo = DateTime.ParseExact(model.ValidToo, "dd-MM-yyyy", null);
                }

                if (model.CouponId > 0) // Update
                {
                    var coupon = _context.Coupons.FirstOrDefault(c => c.CouponId == model.CouponId);
                    if (coupon == null)
                    {
                        TempData["Error"] = "Coupon not found.";
                        return RedirectToAction("Index");
                    }
                    if (coupon != null)
                    {
                        coupon.Code = model.Code;
                        coupon.DiscountType = model.DiscountType;
                        coupon.DiscountValue = model.DiscountValue;
                        coupon.MinOrderAmount = model.MinOrderAmount;
                        coupon.NoOfUsers = model.NoOfUsers;
                        coupon.ValidFrom = model.ValidFrom;
                        coupon.ValidTo = model.ValidTo;
                        coupon.IsActive = model.IsActive;

                        _context.Update(coupon);
                    }
                }
                else // Save
                {
                    _context.Coupons.Add(model);
                }

                _context.SaveChanges();
                TempData["Success"] = "Coupon saved successfully.";
            }
            catch (Exception ex)
            {
                TempData.Remove("Success");
                TempData["Error"] = "Unable to save coupon. Please review the details and try again.";
            }
            return RedirectToAction("Index");
        }






        [HttpPost]
        public IActionResult Delete(int id)
        {
            try
            {
                var coupon = _context.Coupons.FirstOrDefault(c => c.CouponId == id);
                if (coupon == null)
                {
                    return Json(new { success = false, message = "Coupon not found." });
                }

                _context.Coupons.Remove(coupon);
                _context.SaveChanges();

                return Json(new { success = true, message = "Coupon deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
    }
}
