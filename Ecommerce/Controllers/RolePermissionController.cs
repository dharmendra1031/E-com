using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Policy = "SuperAdminOnly")]
    public class RolePermissionController(EcommerceDbContext _context) : Controller
    {
        [HttpGet]
        public IActionResult RoleIndex(int? id, int? roleId)
        {
            if (id.HasValue)
            {
                roleId = _context.RolePermission.AsNoTracking()
                    .Where(p => p.RolePermissionId == id.Value)
                    .Select(p => (int?)p.RoleId).SingleOrDefault();
                if (!roleId.HasValue) return NotFound();
            }

            var arabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
            ViewBag.RoleList = _context.Roles.AsNoTracking().OrderBy(r => r.RoleName)
                .Select(r => new SelectListItem
                {
                    Value = r.RoleId.ToString(),
                    Text = arabic ? (r.RoleNameArb ?? r.RoleName) : r.RoleName
                }).ToList();

            var model = new RolePermissionForm { RoleId = roleId ?? 0 };
            if (!roleId.HasValue) return View(model);
            var selectedRole = _context.Roles.AsNoTracking().SingleOrDefault(r => r.RoleId == roleId.Value);
            if (selectedRole == null) return NotFound();
            var scope = VendorPermissionPages.ScopeFor(selectedRole.RoleName);
            model.PortalScope = scope;

            var existing = _context.RolePermission.AsNoTracking()
                .Where(p => p.RoleId == roleId.Value).ToList();
            if (existing.GroupBy(p => p.PageId).Any(g => g.Count() > 1))
            {
                TempData["Error"] = "Duplicate permission rows must be resolved before editing this role.";
                return View(model);
            }
            var byPage = existing.ToDictionary(p => p.PageId);
            model.Rows = _context.PageMaster.AsNoTracking().Where(p => p.PortalScope == scope).OrderBy(p => p.PageNameEng).ThenBy(p => p.PageId)
                .ToList().Select(p => PermissionRow.From(p, byPage.GetValueOrDefault(p.PageId), arabic)).ToList();
            return View(model);
        }

        [HttpGet]
        public IActionResult CheckRolePermission(int roleId)
        {
            var permission = _context.RolePermission.AsNoTracking()
                .FirstOrDefault(rp => rp.RoleId == roleId);
            return permission == null
                ? Json(new { exists = false })
                : Json(new { exists = true, rolePermissionId = permission.RolePermissionId });
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
        public IActionResult CreateOrUpdatePermission(RolePermissionForm model)
        {
            if (!int.TryParse(User.FindFirst("UserId")?.Value, out var userId) ||
                !_context.Users.Any(u => u.UserId == userId && u.IsActive))
                return Forbid();

            using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);
            var selectedRole = _context.Roles.SingleOrDefault(r => r.RoleId == model.RoleId);
            if (selectedRole == null) return NotFound();
            var scope = VendorPermissionPages.ScopeFor(selectedRole.RoleName);
            var pages = _context.PageMaster.Where(p => p.PortalScope == scope).ToList();
            var pageIds = pages.Select(p => p.PageId).ToList();
            if (!ModelState.IsValid || !model.HasExactPages(pageIds))
            {
                TempData["Error"] = "Invalid or incomplete permission selection. Reload the role and try again.";
                return RedirectToAction(nameof(RoleIndex), new { roleId = model.RoleId });
            }

            if (scope == "Vendor")
            {
                var codes = pages.ToDictionary(p => p.PageId, p => p.PageCode);
                if (model.Rows.Any(row => !row.IsValidVendorGrant(codes[row.PageId], selectedRole.RoleName)))
                {
                    TempData["Error"] = "Unsupported vendor permission.";
                    return RedirectToAction(nameof(RoleIndex), new { roleId = model.RoleId });
                }
            }
            var existing = _context.RolePermission.Where(p => p.RoleId == model.RoleId).ToList();
            if (existing.GroupBy(p => p.PageId).Any(g => g.Count() > 1) ||
                existing.Any(p => !pageIds.Contains(p.PageId)))
            {
                TempData["Error"] = "Invalid existing permission rows must be resolved before saving.";
                return RedirectToAction(nameof(RoleIndex), new { roleId = model.RoleId });
            }

            var byPage = existing.ToDictionary(p => p.PageId);
            var now = DateTime.UtcNow;
            foreach (var row in model.Rows)
            {
                byPage.TryGetValue(row.PageId, out var permission);
                if (!row.HasRights)
                {
                    if (permission != null) _context.RolePermission.Remove(permission);
                    continue;
                }

                if (permission == null)
                {
                    permission = new RolePermission
                    {
                        RoleId = model.RoleId, PageId = row.PageId,
                        CreatedAt = now, CreatedBy = userId
                    };
                    row.ApplyTo(permission);
                    _context.RolePermission.Add(permission);
                }
                else if (!row.Matches(permission))
                {
                    row.ApplyTo(permission);
                    permission.UpdatedAt = now;
                    permission.UpdateBy = userId;
                }
            }

            _context.SaveChanges();
            transaction.Commit();
            TempData["Success"] = "Permissions saved successfully.";
            return RedirectToAction(nameof(PermissionList));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
    }
}
