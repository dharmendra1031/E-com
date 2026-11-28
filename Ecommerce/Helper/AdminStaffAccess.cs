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
    public const string Categories = "admin.categories";
    public const string Subcategories = "admin.subcategories";
    public const string Products = "admin.products";
    public const string TopCategories = "admin.topcategories";
    public const string Stock = "admin.stock";
    public const string Orders = "admin.orders";
    public const string Customers = "admin.customers";
    public const string Areas = "admin.areas";
    public const string Blocks = "admin.blocks";
    public const string Banners = "admin.banners";
    public const string Blog = "admin.blog";
    public const string Coupons = "admin.coupons";
    public const string Deals = "admin.deals";
    public const string Notifications = "admin.notifications";
    public const string Company = "admin.company";
    public const string Contact = "admin.contact";
    public const string About = "admin.about";
    public const string Theme = "admin.theme";
    public const string Reports = "admin.reports";
    public const string Dashboard = "admin.dashboard";
    public static readonly IReadOnlyList<(string Code, string Controller, string Action)> Pages =
        Array.AsReadOnly(new[] { (Brands, "Brand", "Index"), (Categories, "Category", "Index"),
            (Subcategories, "SubCategory", "List"), (Products, "Products", "ProductList"),
            (TopCategories, "Category", "TopCategory"), (Stock, "ProductStock", "Index"), (Orders, "ManageOrders", "PendingOrder"), (Customers, "ManageCustomer", "CustomerList"),
            (Areas, "Areas", "AreaList"), (Blocks, "Blocks", "BlockList"), (Banners, "Banner", "AddBanner"),
            (Blog, "Blog", "BlogList"), (Coupons, "Coupon", "Index"), (Deals, "WekendDeals", "WekendDealsList"),
            (Notifications, "SendNotification", "Index"), (Company, "Companydetails", "Index"), (Contact, "Contact", "ContactList"), (About, "AboutUs", "UpdateAbout"), (Theme, "Changecolors", "Index"), (Reports, "Report", "Index"), (Dashboard, "AdminDashboard", "Index") });

    public static bool IsRestricted(string? role) => role is "AdminManager" or "AdminStaff";
    public static bool IsRestricted(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true && IsRestricted(user.FindFirst(ClaimTypes.Role)?.Value);
    public bool Enabled => environment.IsDevelopment() && configuration.GetValue("Authentication:RestrictedAdminPilotEnabled", false);

    private Task<Dictionary<string, AdminPageRights>>? rights;
    public async Task<AdminPageRights> RightsAsync(ClaimsPrincipal user, string code, CancellationToken ct)
    {
        if (!Pages.Any(p => p.Code == code)) return AdminPageRights.None;
        if (user.Identity is { IsAuthenticated: true, AuthenticationType: CookieAuthenticationDefaults.AuthenticationScheme }
            && user.IsInRole("Admin") && !IsRestricted(user)) return AdminPageRights.Full;
        var grants = await (rights ??= LoadRightsAsync(user, ct));
        return grants.GetValueOrDefault(code, AdminPageRights.None);
    }

    public async Task<(string Controller, string Action)?> FirstPageAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        foreach (var page in Pages)
            if ((await RightsAsync(user, page.Code, ct)).View) return (page.Controller, page.Action);
        return null;
    }

    private async Task<Dictionary<string, AdminPageRights>> LoadRightsAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var none = new Dictionary<string, AdminPageRights>(StringComparer.Ordinal);
        if (user.Identity is not { IsAuthenticated: true, AuthenticationType: CookieAuthenticationDefaults.AuthenticationScheme })
            return none;
        if (!Enabled || !IsRestricted(user) ||
            !int.TryParse(user.FindFirst("UserId")?.Value, out var id) ||
            !int.TryParse(user.FindFirst(RoleIdClaim)?.Value, out var roleId)) return none;
        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        var codes = Pages.Select(p => p.Code).ToArray();
        var grants = await (from u in db.Users.AsNoTracking()
            join rp in db.RolePermission.AsNoTracking() on u.RoleId equals rp.RoleId
            join p in db.PageMaster.AsNoTracking() on rp.PageId equals p.PageId
            where u.UserId == id && u.RoleId == roleId && u.Email == email && u.IsActive &&
                u.Role != null && u.Role.IsActive && u.Role.RoleName == role &&
                p.PortalScope == "Admin" && p.PageCode != null && codes.Contains(p.PageCode)
            select new { p.PageCode, rp.ListPage, rp.CreatePage, rp.UpdatePage, rp.DeletePage, rp.ReportView, rp.ReportPrint }).ToListAsync(ct);
        return grants.Where(g => codes.Contains(g.PageCode, StringComparer.Ordinal))
            .GroupBy(g => g.PageCode!, StringComparer.Ordinal).Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => {
                var grant = g.Single();
                return g.Key == Reports ? AdminPageRights.From(grant.ListPage && grant.ReportView && grant.ReportPrint, false, false, false) :
                    g.Key is Dashboard or Notifications or Company or Contact or Customers or Coupons or Deals ? AdminPageRights.From(grant.ListPage, false, false, false) :
                    g.Key is Areas or Blocks or Banners or About ? AdminPageRights.From(grant.ListPage, grant.CreatePage, grant.UpdatePage, false) :
                    g.Key is Orders or Stock or Theme ? AdminPageRights.From(grant.ListPage, false, grant.UpdatePage, false) :
                    AdminPageRights.From(grant.ListPage, grant.CreatePage, grant.UpdatePage, grant.DeletePage);
            }, StringComparer.Ordinal);
    }
}
