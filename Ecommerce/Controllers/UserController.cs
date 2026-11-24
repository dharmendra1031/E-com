using DocumentFormat.OpenXml.Spreadsheet;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using MF.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
namespace EcommerceWebApplication.Controllers
{
    //[Authorize(Roles = "Admin")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class UserController : Controller
    {
        private readonly HttpClient _client;
        private readonly ApiSettings _apiSettings;
        private readonly EcommerceDbContext _context;
        public UserController(HttpClient client, IOptions<ApiSettings> options, EcommerceDbContext context)
        {
            _apiSettings = options.Value;
            _client = client;
            _context = context;
            _client.BaseAddress = new Uri(_apiSettings.BaseUrl);
        }

        public async Task<IActionResult> UserList(string name, int? roleId, bool? isActive, int page = 1, int pageSize = 10)
        {
           
            var users = await _context.Users.ToListAsync();
            var roles = await _context.Roles.ToListAsync();

           
            var filteredUsers = users.ToList();

            if (!string.IsNullOrEmpty(name))
            {
                filteredUsers = filteredUsers.Where(p =>
                    (!string.IsNullOrEmpty(p.FullName) && p.FullName.Contains(name, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(p.FullNameArb) && p.FullNameArb.Contains(name, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            if (roleId.HasValue)
            {
                filteredUsers = filteredUsers.Where(u => u.RoleId == roleId.Value).ToList();
            }

            if (isActive.HasValue)
            {
                filteredUsers = filteredUsers.Where(u => u.IsActive == isActive.Value).ToList();
            }

         
            var userViewList = filteredUsers.Select(user => new UserViewModel
            {
                UserId = user.UserId,
                FullName = user.FullName,
                FullNameArb = user.FullNameArb,
                Email = user.Email,
                Phone = user.Phone,
                PasswordHash = user.PasswordHash,
                RoleId = user.RoleId,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                UpdatedBy = user.UpdatedBy,
                RoleName = roles.FirstOrDefault(r => r.RoleId == user.RoleId)?.RoleName ?? "N/A"
            }).ToList();

          
            int totalItems = userViewList.Count;

            var pagedData = userViewList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedModel = new PagedResult<UserViewModel>
            {
                Items = pagedData,
                PageNumber = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            ViewBag.RoleList = new SelectList(roles, "RoleId", "RoleName");
            ViewBag.NameFilter = name;
            ViewBag.RoleFilter = roleId;
            ViewBag.IsActiveFilter = isActive;

            return View(pagedModel);
        }
        // GET: User/Create
        public async Task<IActionResult> Create()
        {
            await LoadRolesAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(User user)
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
            {
                return Unauthorized("UserId claim not found.");
            }

            int userId = int.Parse(userIdClaim.Value);
            user.UpdatedBy = userId;

       
            var emailExists = await _context.Users.AnyAsync(u => u.Email == user.Email);
            if (emailExists)
            {
                ModelState.AddModelError(string.Empty, "Email already exists.");
                ViewBag.Error = "Email or Mobile already exists.";
                await LoadRolesAsync();
                return View(user);
            }

        
            var mobileExists = await _context.Users.AnyAsync(u => u.Phone == user.Phone);
            if (mobileExists)
            {
                ModelState.AddModelError(string.Empty, "Mobile number already exists.");
                ViewBag.Error = "Email or Mobile already exists.";
                await LoadRolesAsync();
                return View(user);
            }

            try
            {
            
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                TempData["Success"] = "User saved successfully.";
                return RedirectToAction("UserList");
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "An unexpected error occurred.");
            }

            await LoadRolesAsync();
            return View(user);
        }
        private async Task LoadRolesAsync()
        {
          
            var roles = await _context.Roles.ToListAsync();

            ViewBag.RoleList = new SelectList(roles, "RoleId", "RoleName");
        }

        public async Task<IActionResult> Edit(int id)
        {
          
            var user = await _context.Users.FindAsync(id);

            if (user != null)
            {
                await LoadRolesAsync();
                return View(user);
            }

            return NotFound();
        }


        [HttpPost]
        public async Task<IActionResult> Edit(int id, User user)
        {
            if (id != user.UserId)
            {
                return BadRequest("User ID mismatch.");
            }

           
            var existingUser = await _context.Users.FindAsync(id);

            if (existingUser == null)
            {
                return NotFound();
            }

         
            existingUser.FullName = user.FullName;
            existingUser.FullNameArb = user.FullNameArb;
            existingUser.Email = user.Email;
            existingUser.Phone = user.Phone;
            existingUser.PasswordHash = user.PasswordHash;
            existingUser.RoleId = user.RoleId;
            existingUser.IsActive = user.IsActive;
            existingUser.UpdatedAt = DateTime.UtcNow;
            existingUser.UpdatedBy = user.UpdatedBy;

            await _context.SaveChangesAsync();

          
            TempData["Success"] = "User updated successfully.";

       
            return RedirectToAction("UserList");
        }
        [HttpPost]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {

            var user = await _context.Users.FindAsync(id);

            bool ajax = Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
            if (user == null)
            {
                if (ajax) return Json(new { success = false, message = "User not found." });
                TempData["Error"] = "User not found.";
                return RedirectToAction("UserList");
            }
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            if (ajax) return Json(new { success = true, message = "User deleted successfully." });
            TempData["Success"] = "User deleted successfully.";
            return RedirectToAction("UserList");
        }
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user != null)
            {
                return View(user);
            }

            return NotFound();
        }

        [HttpGet]
        public async Task<JsonResult> AutoCompleteSql(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            IQueryable<User> queryable = _context.Users;

            // Detect if input is Arabic
            bool isArabic = term.Any(c => c >= 0x0600 && c <= 0x06FF);

            if (isArabic)
            {
                queryable = queryable.Where(c => c.FullNameArb.Contains(term));
            }
            else
            {
                queryable = queryable.Where(c => c.FullName.Contains(term));
            }
            var suggestions = await queryable
                .Select(c => isArabic ? c.FullNameArb : c.FullName)
                .Take(10)
                .ToListAsync();
            return Json(suggestions);
        }


    }
}
























































//// GET: User/Delete/5
//public async Task<IActionResult> Delete(int id)
//{
//    var response = await _client.GetAsync($"api/UserApi/{id}");
//    if (response.IsSuccessStatusCode)
//    {
//        var user = await response.Content.ReadFromJsonAsync<User>();
//        return View(user);
//    }

//    return NotFound();
//}

//// POST: User/Delete/5
//[HttpPost]
//public async Task<IActionResult> DeleteConfirmed(int id)
//{
//    var response = await _client.DeleteAsync($"api/UserApi/{id}");
//    return RedirectToAction("UserList");
//}
//// POST: User/Edit/5
//[HttpPost]
//public async Task<IActionResult> Edit(int id, User user)
//{

//    var request = HttpContext.Request;
//    var host = request.Host.Value;
//    var isLocalhost = host.StartsWith("localhost");
//    // Construct the API URL based on the host.        
//    var apiUrl = isLocalhost
//     ? "https://localhost:7239/api/UserApi"
//     : $"https://{host}/api/UserApi";
//    var response = await _client.PutAsJsonAsync($"{apiUrl}/{id}", user);
//    if (response.IsSuccessStatusCode)
//    {
//        TempData["SuccessMessage"] = "Updated Successfully!";

//        // Redirect after successful update to show SweetAlert
//        return RedirectToAction("Edit", new { id = user.UserId });
//    }

//    return View(user);
//}

//// GET: User/Edit/5
//public async Task<IActionResult> Edit(int id)
//{
//    var request = HttpContext.Request;
//    var host = request.Host.Value;
//    var isLocalhost = host.StartsWith("localhost");
//    // Construct the API URL based on the host.        
//    var apiUrl = isLocalhost
//     ? "https://localhost:7239/api/UserApi"
//     : $"https://{host}/api/UserApi";
//    // var response = await _client.GetAsync(apiUrl);
//    var response = await _client.GetAsync($"{apiUrl}/{id}");
//    if (response.IsSuccessStatusCode)
//    {
//        await LoadRolesAsync();
//        //  await LoadUsersAsync(); // Load users into ViewBag
//        var user = await response.Content.ReadFromJsonAsync<User>();
//        return View(user);
//    }

//    return NotFound();
//}
//public async Task<IActionResult> UserList(string name, int? roleId, bool? isActive, int page = 1, int pageSize = 10)

//{
//    var request = HttpContext.Request;
//    var host = request.Host.Value;
//    var isLocalhost = host.StartsWith("localhost");
//    // Construct the API URL based on the host.        
//    var apiUrl = isLocalhost
//    ? "https://localhost:7239/api/RoleApi"
//    : $"https://{host}/admin/api/RoleApi";
//    var roleResponse = await _client.GetAsync(apiUrl);
//    apiUrl = isLocalhost
//    ? "https://localhost:7239/api/UserApi"
//    : $"https://{host}/admin/api/UserApi";
//    var userResponse = await _client.GetAsync(apiUrl);
//    //  var roleResponse = await _client.GetAsync("/admin/api/RoleApi");

//    if (userResponse.IsSuccessStatusCode && roleResponse.IsSuccessStatusCode)
//    {
//        var users = await userResponse.Content.ReadFromJsonAsync<List<UserViewModel>>();
//        var roles = await roleResponse.Content.ReadFromJsonAsync<List<Role>>();

//        // Filter data
//        var filteredUsers = users.ToList();

//        if (!string.IsNullOrEmpty(name))
//        {
//            filteredUsers = filteredUsers.Where(p =>
//                (!string.IsNullOrEmpty(p.FullName) && p.FullName.Contains(name, StringComparison.OrdinalIgnoreCase)) ||
//                (!string.IsNullOrEmpty(p.FullNameArb) && p.FullNameArb.Contains(name, StringComparison.OrdinalIgnoreCase))
//            ).ToList();
//        }

//        if (roleId.HasValue)
//            filteredUsers = filteredUsers.Where(u => u.RoleId == roleId.Value).ToList();

//        if (isActive.HasValue)
//            filteredUsers = filteredUsers.Where(u => u.IsActive == isActive.Value).ToList();

//        // Set role name
//        foreach (var user in filteredUsers)
//        {
//            user.RoleName = roles.FirstOrDefault(r => r.RoleId == user.RoleId)?.RoleName ?? "N/A";
//        }
//        int totalItems = filteredUsers.Count;
//        var pagedData = filteredUsers
//            .Skip((page - 1) * pageSize)
//            .Take(pageSize)
//            .ToList();

//        var pagedModel = new PagedResult<UserViewModel>
//        {
//            Items = pagedData,
//            PageNumber = page,
//            PageSize = pageSize,
//            TotalItems = totalItems
//        };
//        // 👇 Very important — set ViewBag values so they persist after search
//        ViewBag.RoleList = new SelectList(roles, "RoleId", "RoleName");
//        ViewBag.NameFilter = name;
//        ViewBag.RoleFilter = roleId;
//        ViewBag.IsActiveFilter = isActive;

//        return View(pagedModel);
//    }


//    ViewBag.RoleList = new SelectList(new List<Role>(), "RoleId", "RoleName");
//    return View(new List<UserViewModel>());
//}


//public async Task<IActionResult> Index()
//{
//    var request = HttpContext.Request;
//    var host = request.Host.Value;
//    var isLocalhost = host.StartsWith("localhost");
//    // Construct the API URL based on the host.        
//    var apiUrl = isLocalhost
//     ? "https://localhost:7239/admin/api/UserApi"
//     : $"https://{host}/admin/api/UserApi";
//    var response = await _client.GetAsync(apiUrl);
//    //var response = await _client.GetAsync("api/UserApi");
//    if (response.IsSuccessStatusCode)
//    {
//        var users = await response.Content.ReadFromJsonAsync<List<User>>();
//        return View(users);
//    }

//    return View(new List<User>());
//}


// POST: User/Create
//[HttpPost]
//public async Task<IActionResult> Create(User user)
//{
//    if (!ModelState.IsValid)
//        return View(user);

//    var userIdClaim = User.FindFirst("UserId");
//    if (userIdClaim == null)
//    {
//        return Unauthorized("UserId claim not found.");
//    }

//    int userId = int.Parse(userIdClaim.Value);
//    user.UpdatedBy = userId;

//    var response = await _client.PostAsJsonAsync("api/UserApi", user);

//    if (response.IsSuccessStatusCode)
//    {
//        TempData["SuccessMessage"] = "Saved Successfully!";

//    }
//    else if (response.StatusCode == HttpStatusCode.Conflict)
//    {
//        var error = await response.Content.ReadAsStringAsync();
//        ModelState.AddModelError(string.Empty, error);
//        ViewBag.Error = "Email or Mobile already exists.";
//    }
//    else
//    {
//        ModelState.AddModelError(string.Empty, "An unexpected error occurred.");
//    }

//    // Load Role list again for dropdown in case of failure
//    await LoadRolesAsync();
//    return View(user);
//}

//[HttpPost]
//public async Task<IActionResult> Create(User user)
//{


//    var userIdClaim = User.FindFirst("UserId");
//    if (userIdClaim == null)
//    {
//        return Unauthorized("UserId claim not found.");
//    }

//    int userId = int.Parse(userIdClaim.Value);
//    user.UpdatedBy = userId;


//    var request = HttpContext.Request;
//    var host = request.Host.Value;
//    var isLocalhost = host.StartsWith("localhost", StringComparison.OrdinalIgnoreCase);


//    var apiUrl = isLocalhost
//        ? "https://localhost:7239/api/UserApi"
//        : $"https://{host}/api/UserApi";


//    var response = await _client.PostAsJsonAsync(apiUrl, user);


//    if (response.IsSuccessStatusCode)
//    {
//        TempData["SuccessMessage"] = "Saved Successfully!";
//        return RedirectToAction("UserList");
//    }
//    else if (response.StatusCode == HttpStatusCode.Conflict)
//    {
//        var error = await response.Content.ReadAsStringAsync();
//        ModelState.AddModelError(string.Empty, error);
//        ViewBag.Error = "Email or Mobile already exists.";
//    }
//    else
//    {
//        ModelState.AddModelError(string.Empty, "An unexpected error occurred.");
//    }


//    await LoadRolesAsync();
//    return View(user);
//}
//private async Task LoadRolesAsync()
//{
//    var request = HttpContext.Request;
//    var host = request.Host.Value;
//    var isLocalhost = host.StartsWith("localhost");
//    // Construct the API URL based on the host.        
//    var apiUrl = isLocalhost
//     ? "https://localhost:7239/api/RoleApi"
//     : $"https://{host}/api/RoleApi";
//    var response = await _client.GetAsync(apiUrl);
//    //var response = await _client.GetAsync("api/RoleApi");
//    var roles = response.IsSuccessStatusCode
//        ? await response.Content.ReadFromJsonAsync<List<Role>>()
//        : new List<Role>();

//    ViewBag.RoleList = new SelectList(roles, "RoleId", "RoleName");
//}

//private async Task LoadUsersAsync()
//{
//    var request = HttpContext.Request;
//    var host = request.Host.Value;
//    var isLocalhost = host.StartsWith("localhost");
//    // Construct the API URL based on the host.        
//    var apiUrl = isLocalhost
//     ? "https://localhost:7239/api/UserApi"
//     : $"https://{host}/api/UserApi";
//    var response = await _client.GetAsync(apiUrl);
//    //var response = await _client.GetAsync("api/UserApi"); // Ensure this endpoint returns a list of users
//    var users = response.IsSuccessStatusCode
//        ? await response.Content.ReadFromJsonAsync<List<User>>()
//        : new List<User>();

//    ViewBag.UserList = users;
//}