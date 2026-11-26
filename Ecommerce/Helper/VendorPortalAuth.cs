using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EcommerceWebApplication.Helper;

public static class VendorPortalAuth
{
    public const string Scheme = "VendorPortal";
    public const string Policy = "VendorPortalOnly";
    public const string LoginRatePolicy = "VendorLogin";
    public const string CredentialClaim = "VendorCredentialVersion";

    public static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = "__Secure-EcomVendor";
        options.Cookie.Path = "/vendor-portal";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/vendor-portal/login";
        options.AccessDeniedPath = "/vendor-portal/denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(3);
        options.SlidingExpiration = false;
    }

    public static string CredentialVersion(string hash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash)));

    public static bool TryGetUserId(ClaimsPrincipal? principal, out int id)
    {
        id = 0;
        return principal?.Identity is { IsAuthenticated: true, AuthenticationType: Scheme } &&
            int.TryParse(principal.FindFirst("UserId")?.Value, out id) && id > 0;
    }
}
