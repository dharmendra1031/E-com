using EcommerceWebApplication.Helper;

const string password = "Example-Test-Passphrase";
void Check(bool success, string message) { if (!success) throw new Exception(message); }
var first = StaffPassword.Hash(password);
var second = StaffPassword.Hash(password);
Check(first != password && first != second, "Hash must be salted.");
Check(StaffPassword.Verify(first, password, false, out var upgrade) && !upgrade, "Valid password rejected.");
Check(!StaffPassword.Verify(first, "wrong", true, out _), "Incorrect password accepted.");
Check(!StaffPassword.Verify(first, first, true, out _), "Stored hash accepted as password.");
Check(!StaffPassword.Verify(password, password, false, out _), "Legacy password accepted when disabled.");
Check(StaffPassword.Verify(password, password, true, out upgrade) && upgrade, "Legacy upgrade not requested.");
Check(!StaffPassword.Verify(password, password.ToLowerInvariant(), true, out _), "Legacy case mismatch accepted.");
Check(!StaffPassword.Verify(StaffPassword.Prefix + "invalid", StaffPassword.Prefix + "invalid", true, out _), "Malformed hash accepted.");
Check(!StaffPassword.IsValidNew("short") && StaffPassword.IsValidNew(password), "Password length validation incorrect.");
Console.WriteLine("Staff password checks passed.");

var cookie = new Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions();
VendorPortalAuth.ConfigureCookie(cookie);
Check(cookie.Cookie.Name == "__Secure-EcomVendor" && cookie.Cookie.Path == "/vendor-portal", "Vendor cookie boundary incorrect.");
Check(cookie.Cookie.HttpOnly && cookie.Cookie.SecurePolicy == Microsoft.AspNetCore.Http.CookieSecurePolicy.Always &&
    !cookie.SlidingExpiration, "Unsafe vendor cookie configuration.");
System.Security.Claims.ClaimsPrincipal Principal(string scheme, string id) => new(new System.Security.Claims.ClaimsIdentity(
    new[] { new System.Security.Claims.Claim("UserId", id) }, scheme));
Check(VendorPortalAuth.TryGetUserId(Principal(VendorPortalAuth.Scheme, "1"), out var vendorUserId) && vendorUserId == 1, "Vendor identity rejected.");
Check(!VendorPortalAuth.TryGetUserId(Principal("Cookies", "1"), out _), "Default admin/customer identity accepted.");
Check(!VendorPortalAuth.TryGetUserId(Principal(VendorPortalAuth.Scheme, "0"), out _), "Invalid ID accepted.");
Check(VendorPortalAuth.CredentialVersion(first) != VendorPortalAuth.CredentialVersion(second), "Credential rotation not reflected.");
Check(!EcommerceWebApplication.Models.VendorRoles.IsVendorRole("Admin") &&
    EcommerceWebApplication.Models.VendorRoles.IsVendorRole("VendorOwner"), "Vendor role boundary incorrect.");
Console.WriteLine("Vendor portal authentication configuration checks passed.");
