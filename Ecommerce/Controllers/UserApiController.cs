using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = "SuperAdminOnly")]
public class UserApiController(EcommerceDbContext db) : ControllerBase
{
    private IQueryable<UserViewModel> Users => db.Users.AsNoTracking().Select(u => new UserViewModel
    {
        UserId = u.UserId, FullName = u.FullName, FullNameArb = u.FullNameArb, Email = u.Email,
        Phone = u.Phone, RoleId = u.RoleId, IsActive = u.IsActive, CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt, UpdatedBy = u.UpdatedBy, RoleName = u.Role == null ? "N/A" : u.Role.RoleName!
    });

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserViewModel>>> GetUsers() => await Users.ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<UserViewModel>> GetUser(int id)
    {
        var user = await Users.SingleOrDefaultAsync(u => u.UserId == id);
        return user == null ? NotFound() : Ok(user);
    }

    // Legacy duplicate write routes are retired; use the protected admin User screen.
    [HttpPost]
    [HttpPut("{id}")]
    [HttpDelete("{id}")]
    public IActionResult RetiredWrite() => StatusCode(StatusCodes.Status405MethodNotAllowed);
}
