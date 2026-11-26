using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceWebApplication.Controllers;

[Authorize(Policy = "SuperAdminOnly")]
[AutoValidateAntiforgeryToken]
public class UserController(EcommerceDbContext db) : Controller
{
    public async Task<IActionResult> UserList(string? name, int? roleId, bool? isActive, int page = 1, int pageSize = 10)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(u => (u.FullName != null && u.FullName.Contains(name)) ||
                (u.FullNameArb != null && u.FullNameArb.Contains(name)));
        if (roleId.HasValue) query = query.Where(u => u.RoleId == roleId.Value);
        if (isActive.HasValue) query = query.Where(u => u.IsActive == isActive.Value);
        var count = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(count / (double)pageSize)));
        var users = await query.OrderBy(u => u.UserId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new UserViewModel
            {
                UserId = u.UserId, FullName = u.FullName, FullNameArb = u.FullNameArb, Email = u.Email,
                Phone = u.Phone, RoleId = u.RoleId, IsActive = u.IsActive, CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt, UpdatedBy = u.UpdatedBy, RoleName = u.Role == null ? "N/A" : u.Role.RoleName!
            }).ToListAsync();
        ViewBag.RoleList = new SelectList(await db.Roles.AsNoTracking().ToListAsync(), "RoleId", "RoleName");
        ViewBag.NameFilter = name;
        ViewBag.RoleFilter = roleId;
        ViewBag.IsActiveFilter = isActive;
        return View(new PagedResult<UserViewModel> { Items = users, PageNumber = page, PageSize = pageSize, TotalItems = count });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadRolesAsync();
        return View(new StaffUserForm());
    }

    [HttpPost]
    public async Task<IActionResult> Create(StaffUserForm form)
    {
        if (!int.TryParse(User.FindFirstValue("UserId"), out var actorId)) return Forbid();
        if (!StaffPassword.IsValidNew(form.PasswordHash))
            ModelState.AddModelError(nameof(form.PasswordHash), "Password must contain 12 to 128 characters.");
        await ValidateDetails(form, 0);
        if (!ModelState.IsValid) return await InvalidForm(form);
        var user = new User { CreatedAt = DateTime.UtcNow };
        form.ApplyTo(user, actorId);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TempData["Success"] = "User saved successfully.";
        return RedirectToAction(nameof(UserList));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound();
        await LoadRolesAsync();
        return View(StaffUserForm.FromUser(user));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, StaffUserForm form)
    {
        if (id != form.UserId) return BadRequest();
        if (!int.TryParse(User.FindFirstValue("UserId"), out var actorId)) return Forbid();
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (!string.IsNullOrEmpty(form.PasswordHash) && !StaffPassword.IsValidNew(form.PasswordHash))
            ModelState.AddModelError(nameof(form.PasswordHash), "Password must contain 12 to 128 characters.");
        if (actorId == id && (!form.IsActive || form.RoleId != user.RoleId || form.Email != user.Email))
            ModelState.AddModelError("", "You cannot deactivate or change your own role or email.");
        await ValidateDetails(form, id);
        if (!ModelState.IsValid) return await InvalidForm(form);
        form.ApplyTo(user, actorId);
        await db.SaveChangesAsync();
        TempData["Success"] = "User updated successfully.";
        return RedirectToAction(nameof(UserList));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!int.TryParse(User.FindFirstValue("UserId"), out var actorId) || actorId == id) return Forbid();
        var user = await db.Users.FindAsync(id);
        var ajax = Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
        if (user == null)
        {
            if (ajax) return Json(new { success = false, message = "User not found." });
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(UserList));
        }
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        if (ajax) return Json(new { success = true, message = "User deleted successfully." });
        TempData["Success"] = "User deleted successfully.";
        return RedirectToAction(nameof(UserList));
    }

    [HttpGet]
    public async Task<JsonResult> AutoCompleteSql(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return Json(Array.Empty<string>());
        bool ar = term.Any(c => c >= 0x0600 && c <= 0x06ff);
        var query = db.Users.AsNoTracking();
        var names = ar ? query.Select(u => u.FullNameArb) : query.Select(u => u.FullName);
        return Json(await names.Where(n => n != null && n.Contains(term)).Distinct().Take(10).ToListAsync());
    }

    private async Task ValidateDetails(StaffUserForm form, int id)
    {
        if (await db.Users.AnyAsync(u => u.UserId != id && u.Email == form.Email))
            ModelState.AddModelError(nameof(form.Email), "Email already exists.");
        if (await db.Users.AnyAsync(u => u.UserId != id && u.Phone == form.Phone))
            ModelState.AddModelError(nameof(form.Phone), "Mobile number already exists.");
        if (!await db.Roles.AnyAsync(r => r.RoleId == form.RoleId && r.IsActive))
            ModelState.AddModelError(nameof(form.RoleId), "Choose an active role.");
    }

    private async Task<IActionResult> InvalidForm(StaffUserForm form)
    {
        form.PasswordHash = null;
        await LoadRolesAsync();
        return View(form);
    }

    private async Task LoadRolesAsync() =>
        ViewBag.RoleList = new SelectList(await db.Roles.AsNoTracking().Where(r => r.IsActive).ToListAsync(), "RoleId", "RoleName");
}
