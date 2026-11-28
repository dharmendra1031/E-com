using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace EcommerceWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "SuperAdminOnly")]
    [AutoValidateAntiforgeryToken]
    public class RoleApiController : ControllerBase
    {
        private readonly EcommerceDbContext _context;

        public RoleApiController(EcommerceDbContext context)
        {
            _context = context;
        }
        // GET: api/RoleApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Role>>> GetRoles()
        {
            return await _context.Roles.ToListAsync();
        }

        // GET: api/RoleApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Role>> GetRole(int id)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role == null)
                return NotFound();
            return role;
        }

        private static bool ReservedVendorName(string? name) =>
            new[] { VendorRoles.Owner, VendorRoles.Manager, VendorRoles.Staff }
                .Any(r => string.Equals(r, name?.Trim(), StringComparison.OrdinalIgnoreCase));

        // POST: api/RoleApi
        [HttpPost]
        public async Task<ActionResult<Role>> CreateRole(Role role)
        {
            if (ReservedVendorName(role.RoleName)) return BadRequest("Vendor roles are managed by the role seed.");
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRole), new { id = role.RoleId }, role);
        }

        // PUT: api/RoleApi/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRole(int id, Role role)
        {
            if (id != role.RoleId)
                return BadRequest("Role ID mismatch.");

            var existingRole = await _context.Roles.FindAsync(id);
            if (existingRole == null)
                return NotFound();

            if ((ReservedVendorName(existingRole.RoleName) || ReservedVendorName(role.RoleName)) &&
                !string.Equals(existingRole.RoleName, role.RoleName, StringComparison.Ordinal))
                return BadRequest("Reserved vendor role names cannot be changed.");
            existingRole.RoleName = role.RoleName;
            existingRole.RoleNameArb = role.RoleNameArb;
            existingRole.IsActive = role.IsActive;
            existingRole.UpdatedAt = DateTime.UtcNow;
            existingRole.UpdatedBy = role.UpdatedBy;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/RoleApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role == null)
                return NotFound();

            if (ReservedVendorName(role.RoleName)) return BadRequest("Deactivate vendor roles instead of deleting them.");
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
