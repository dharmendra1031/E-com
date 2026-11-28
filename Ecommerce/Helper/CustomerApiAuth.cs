using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace EcommerceWebApplication.Helper;

public static class CustomerApiAuth
{
    public const string Scheme = "CustomerApi";
    private const string VersionClaim = "CustomerCredentialVersion";

    public static AuthenticationTicket CreateTicket(int userId, string credential, DateTimeOffset now, TimeSpan lifetime)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("UserId", userId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Role, "Customer"),
            new Claim(VersionClaim, VendorPortalAuth.CredentialVersion(credential))
        }, Scheme);
        return new AuthenticationTicket(new ClaimsPrincipal(identity),
            new AuthenticationProperties { IssuedUtc = now, ExpiresUtc = now.Add(lifetime) }, Scheme);
    }

    public static bool TryGetUserId(ClaimsPrincipal? principal, out int id)
    {
        id = 0;
        return principal?.Identity is { IsAuthenticated: true, AuthenticationType: Scheme } &&
            principal.IsInRole("Customer") && int.TryParse(principal.FindFirst("UserId")?.Value, out id) && id > 0;
    }

    public static bool IsCurrent(ClaimsPrincipal principal, string credential) =>
        TryGetUserId(principal, out _) &&
        principal.FindFirst(VersionClaim)?.Value == VendorPortalAuth.CredentialVersion(credential);
}
