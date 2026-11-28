using EcommerceWebApplication.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceWebApplication.Helper;

public sealed record AdminPageRights(bool View, bool Create, bool Edit, bool Delete)
{
    public static readonly AdminPageRights None = new(false, false, false, false);
    public static readonly AdminPageRights Full = new(true, true, true, true);
    public static AdminPageRights From(bool view, bool create, bool edit, bool delete) =>
        view ? new(true, create, edit, delete) : None;
}

public sealed class AdminStaffAccess(EcommerceDbContext db, IConfiguration configuration, IWebHostEnvironment environment)
{
    public const string Roles = "Admin,AdminManager,AdminStaff";
    public const string CredentialClaim = "AdminStaffCredentialVersion";
    public const string RoleIdClaim = "AdminStaffRoleId";
    public const string Brands = "admin.brands";

    public static bool IsRestricted(string? role) => role is "AdminManager" or "AdminStaff";
    public static bool IsRestricted(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true && IsRestricted(user.FindFirst(ClaimTypes.Role)?.Value);
    public bool Enabled => environment.IsDevelopment() && configuration.GetValue("Authentication:RestrictedAdminPilotEnabled", false);

    private Task<AdminPageRights>? brandRights;
    public Task<AdminPageRights> BrandRightsAsync(ClaimsPrincipal user, CancellationToken ct) =>
        brandRights ??= LoadBrandRightsAsync(user, ct);

    private async Task<AdminPageRights> LoadBrandRightsAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        if (user.Identity is not { IsAuthenticated: true, AuthenticationType: CookieAuthenticationDefaults.AuthenticationScheme })
            return AdminPageRights.None;
        if (user.IsInRole("Admin") && !IsRestricted(user)) return AdminPageRights.Full;
        if (!Enabled || !IsRestricted(user) ||
            !int.TryParse(user.FindFirst("UserId")?.Value, out var id) ||
            !int.TryParse(user.FindFirst(RoleIdClaim)?.Value, out var roleId)) return AdminPageRights.None;
        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        var grants = await (from u in db.Users.AsNoTracking()
            join rp in db.RolePermission.AsNoTracking() on u.RoleId equals rp.RoleId
            join p in db.PageMaster.AsNoTracking() on rp.PageId equals p.PageId
            where u.UserId == id && u.RoleId == roleId && u.Email == email && u.IsActive &&
                u.Role != null && u.Role.IsActive && u.Role.RoleName == role &&
                p.PortalScope == "Admin" && p.PageCode == Brands
            select new { rp.ListPage, rp.CreatePage, rp.UpdatePage, rp.DeletePage }).Take(2).ToListAsync(ct);
        return grants.Count == 1 ? AdminPageRights.From(grants[0].ListPage, grants[0].CreatePage,
            grants[0].UpdatePage, grants[0].DeletePage) : AdminPageRights.None;
    }
}
