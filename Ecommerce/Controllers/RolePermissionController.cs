using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Policy = "SuperAdminOnly")]
    public class RolePermissionController : Controller
    {
        private readonly EcommerceDbContext _context;

        public RolePermissionController(EcommerceDbContext context)
        {
            _context = context;
        }

        #region RolePermission
        //Role Permission Code Here 
        public IActionResult RoleIndex(int? Id, int? roleId)
        {
            ViewBag.RoleList = _context.Roles
                .Select(r => new SelectListItem
                {
                    Value = r.RoleId.ToString(),
                    Text = r.RoleName
                }).ToList();

            ViewBag.PageList = _context.PageMaster
                .Select(p => new SelectListItem
                {
                    Value = p.PageId.ToString(),
                    Text = p.PageNameEng
                }).ToList();

            if (Id.HasValue)
            {
                var existingPermission = _context.RolePermission
                    .FirstOrDefault(rp => rp.RolePermissionId == Id.Value);

                if (existingPermission == null)
                    return NotFound();

                var roleIdFromDb = existingPermission.RoleId;

                var permissions = _context.RolePermission
                    .Where(rp => rp.RoleId == roleIdFromDb)
                    .ToList();

                var model = new RolePermission
                {
                    RoleId = roleIdFromDb,
                    PageIds = permissions.Select(p => p.PageId).ToList(),
                    CreatePage = permissions.Any(p => p.CreatePage),
                    UpdatePage = permissions.Any(p => p.UpdatePage),
                    DeletePage = permissions.Any(p => p.DeletePage),
                    ListPage = permissions.Any(p => p.ListPage),
                    ReportView = permissions.Any(p => p.ReportView),
                    ReportPrint = permissions.Any(p => p.ReportPrint),
                    RolePermissionId = Id.Value
                };

                return View(model);
            }

            // ✅ Create mode with pre-selected role
            var newModel = new RolePermission();
            if (roleId.HasValue)
            {
                newModel.RoleId = roleId.Value;
            }

            return View(newModel);
        }
        [HttpGet]
        public IActionResult CheckRolePermission(int roleId)
        {
            var permission = _context.RolePermission
                .FirstOrDefault(rp => rp.RoleId == roleId);

            if (permission != null)
            {
                return Json(new { exists = true, rolePermissionId = permission.RolePermissionId });
            }

            return Json(new { exists = false });
        }

        public IActionResult PermissionList(int page = 1, int pageSize = 10)
        {
            var list = (from rp in _context.RolePermission
                        join r in _context.Roles on rp.RoleId equals r.RoleId
                        join p in _context.PageMaster on rp.PageId equals p.PageId
                        select new PermissionListViewModel
                        {
                            RolePermissionId = rp.RolePermissionId,
                            RoleName = r.RoleName,
                            PageName = p.PageNameEng,
                            CreatePage = rp.CreatePage ? 1 : 0,
                            UpdatePage = rp.UpdatePage ? 1 : 0,
                            DeletePage = rp.DeletePage ? 1 : 0,
                            ListPage = rp.ListPage ? 1 : 0,
                            ReportView = rp.ReportView ? 1 : 0,
                            ReportPrint = rp.ReportPrint ? 1 : 0,
                            CreatedAt = rp.CreatedAt
                        }).ToList();
            int totalItems = list.Count;
            var pagedData = list
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedModel = new PagedResult<PermissionListViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
            return View(pagedModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateOrUpdatePermission(RolePermission model)
        {
            
            if (model.PageIds == null || !model.PageIds.Any())
            {
                var allPermissions = _context.RolePermission
                    .Where(rp => rp.RoleId == model.RoleId)
                    .ToList();

                if (allPermissions.Any())
                {
                    _context.RolePermission.RemoveRange(allPermissions);
                    _context.SaveChanges();
                    TempData["SuccessMessage"] = "All permissions removed for this role successfully!";
                }

                return RedirectToAction("RoleIndex");
            }

           
            bool anyUpdated = false, anyCreated = false;

            
            var existingPermissions = _context.RolePermission
                .Where(rp => rp.RoleId == model.RoleId)
                .ToList();

           
            var toDelete = existingPermissions
                .Where(rp => !model.PageIds.Contains(rp.PageId))
                .ToList();

            if (toDelete.Any())
            {
                _context.RolePermission.RemoveRange(toDelete);
            }

            foreach (var pageId in model.PageIds)
            {
                var existing = existingPermissions.FirstOrDefault(rp => rp.PageId == pageId);

                if (existing != null)
                {
                  
                    existing.CreatePage = model.CreatePage;
                    existing.UpdatePage = model.UpdatePage;
                    existing.DeletePage = model.DeletePage;
                    existing.ListPage = model.ListPage;
                    existing.ReportView = model.ReportView;
                    existing.ReportPrint = model.ReportPrint;
                    existing.UpdatedAt = DateTime.Now;
                    existing.UpdateBy = 1;

                    _context.RolePermission.Update(existing);
                    anyUpdated = true;
                }
                else
                {
                    
                    var newPermission = new RolePermission
                    {
                        RoleId = model.RoleId,
                        PageId = pageId,
                        CreatePage = model.CreatePage,
                        UpdatePage = model.UpdatePage,
                        DeletePage = model.DeletePage,
                        ListPage = model.ListPage,
                        ReportView = model.ReportView,
                        ReportPrint = model.ReportPrint,
                        CreatedAt = DateTime.Now,
                        CreatedBy = 1
                    };

                    _context.RolePermission.Add(newPermission);
                    anyCreated = true;
                }
            }

          
            _context.SaveChanges();

           
            if (anyUpdated && anyCreated)
                TempData["SuccessMessage"] = "Permissions updated and new entries added successfully!";
            else if (anyUpdated)
                TempData["SuccessMessage"] = "Permissions updated successfully!";
            else if (anyCreated)
                TempData["SuccessMessage"] = "Permissions saved successfully!";
            else if (toDelete.Any())
                TempData["SuccessMessage"] = "Old permissions removed successfully!";

            return RedirectToAction("RoleIndex");
        }


        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult CreateOrUpdatePermission(RolePermission model)
        //{
        //    if (model.PageIds != null && model.PageIds.Any())
        //    {
        //        bool anyUpdated = false, anyCreated = false;

        //        foreach (var pageId in model.PageIds)
        //        {
        //            var existing = _context.RolePermission
        //                .FirstOrDefault(rp => rp.RoleId == model.RoleId && rp.PageId == pageId);

        //            if (existing != null)
        //            {
        //                existing.CreatePage = model.CreatePage;
        //                existing.UpdatePage = model.UpdatePage;
        //                existing.DeletePage = model.DeletePage;
        //                existing.ListPage = model.ListPage;
        //                existing.ReportView = model.ReportView;
        //                existing.ReportPrint = model.ReportPrint;
        //                existing.UpdatedAt = DateTime.Now;
        //                existing.UpdateBy = 1;

        //                _context.RolePermission.Update(existing);
        //                anyUpdated = true;
        //            }
        //            else
        //            {
        //                var newPermission = new RolePermission
        //                {
        //                    RoleId = model.RoleId,
        //                    PageId = pageId,
        //                    CreatePage = model.CreatePage,
        //                    UpdatePage = model.UpdatePage,
        //                    DeletePage = model.DeletePage,
        //                    ListPage = model.ListPage,
        //                    ReportView = model.ReportView,
        //                    ReportPrint = model.ReportPrint,
        //                    CreatedAt = DateTime.Now,
        //                    CreatedBy = 1
        //                };

        //                _context.RolePermission.Add(newPermission);
        //                anyCreated = true;
        //            }
        //        }

        //        _context.SaveChanges();

        //        if (anyUpdated && anyCreated)
        //            TempData["SuccessMessage"] = "Permissions updated and new entries added successfully!";
        //        else if (anyUpdated)
        //            TempData["SuccessMessage"] = "Permissions updated successfully!";
        //        else if (anyCreated)
        //            TempData["SuccessMessage"] = "Permissions saved successfully!";
        //    }

        //    return RedirectToAction("RoleIndex");
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult CreateOrUpdatePermission(RolePermission model)
        //{
        //    if (model.PageIds != null && model.PageIds.Any())
        //    {
        //        foreach (var pageId in model.PageIds)
        //        {
        //            var existing = _context.RolePermission
        //                .FirstOrDefault(rp => rp.RoleId == model.RoleId && rp.PageId == pageId);

        //            if (existing != null)
        //            {
        //                existing.CreatePage = model.CreatePage;
        //                existing.UpdatePage = model.UpdatePage;
        //                existing.DeletePage = model.DeletePage;
        //                existing.ListPage = model.ListPage;
        //                existing.ReportView = model.ReportView;
        //                existing.ReportPrint = model.ReportPrint;
        //                existing.UpdatedAt = DateTime.Now;
        //                existing.UpdateBy = 1;

        //                _context.RolePermission.Update(existing);
        //            }
        //            else
        //            {
        //                var newPermission = new RolePermission
        //                {
        //                    RoleId = model.RoleId,
        //                    PageId = pageId,
        //                    CreatePage = model.CreatePage,
        //                    UpdatePage = model.UpdatePage,
        //                    DeletePage = model.DeletePage,
        //                    ListPage = model.ListPage,
        //                    ReportView = model.ReportView,
        //                    ReportPrint = model.ReportPrint,
        //                    CreatedAt = DateTime.Now,
        //                    CreatedBy = 1
        //                };

        //                _context.RolePermission.Add(newPermission);
        //            }
        //        }

        //        _context.SaveChanges();
        //        TempData["SuccessMessage"] = "Permissions saved/updated successfully!";
        //    }

        //    return RedirectToAction("RoleIndex");
        //}

        [HttpPost]
        public IActionResult Delete(int id)
        {
            try
            {
                var rolepermission = _context.RolePermission.FirstOrDefault(c => c.RolePermissionId == id);
                if (rolepermission == null)
                {
                    return Json(new { success = false, message = "rolepermission not found!" });
                }

                _context.RolePermission.Remove(rolepermission);
                _context.SaveChanges();

                return Json(new { success = true, message = "✅ Role Permission successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Can not delete Role " });
            }
        }
        #endregion
    }
}
