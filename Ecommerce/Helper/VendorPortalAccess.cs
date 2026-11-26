using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Helper;

public class VendorPortalAccess(EcommerceDbContext db, IConfiguration configuration)
{
    public bool Enabled => configuration.GetValue("Marketplace:VendorPortalEnabled", false)
        && configuration["Marketplace:VendorMasterEnabled"] == "true";

    public IQueryable<VendorUser> Memberships(int userId) =>
        db.VendorUsers.AsNoTracking().Where(m => m.UserId == userId && m.IsActive &&
            m.Vendor.Status == "Approved" && m.User.IsActive && m.User.Role != null && m.User.Role.IsActive &&
            (m.User.Role.RoleName == VendorRoles.Owner || m.User.Role.RoleName == VendorRoles.Manager ||
             m.User.Role.RoleName == VendorRoles.Staff));

    public async Task<bool> IsCurrentAsync(System.Security.Claims.ClaimsPrincipal? principal, CancellationToken ct)
    {
        if (!Enabled || !VendorPortalAuth.TryGetUserId(principal, out var id)) return false;
        var user = await Memberships(id).Select(m => new { m.User.PasswordHash, m.User.Email, m.User.Role!.RoleName })
            .FirstOrDefaultAsync(ct);
        if (user?.PasswordHash == null || !user.PasswordHash.StartsWith(StaffPassword.Prefix, StringComparison.Ordinal))
            return false;
        return principal!.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value == user.Email &&
            principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value == user.RoleName &&
            principal.FindFirst(VendorPortalAuth.CredentialClaim)?.Value == VendorPortalAuth.CredentialVersion(user.PasswordHash);
    }
}
