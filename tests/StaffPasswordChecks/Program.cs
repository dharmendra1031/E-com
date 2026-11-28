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

var protector = new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider().CreateProtector("test-quote");
var checkoutQuote = new SellerCheckoutQuote(Guid.NewGuid(), 7, DateTime.UtcNow.AddMinutes(10), 3m, 5m,
    new[] { new SellerQuoteLine(1, 1, 101m), new SellerQuoteLine(2, 2, 95m) });
Check(checkoutQuote.IsValid() && checkoutQuote.Subtotal == 291m && checkoutQuote.VatAmount == 14.55m &&
    checkoutQuote.Total == 308.55m, "Checkout arithmetic incorrect.");
var protectedQuote = checkoutQuote.Protect(protector);
Check(SellerCheckoutQuote.Read(protector, protectedQuote, 7)?.RequestId == checkoutQuote.RequestId, "Quote could not be recovered.");
Check(SellerCheckoutQuote.Read(protector, protectedQuote, 8) == null, "Cross-customer quote accepted.");
Check(SellerCheckoutQuote.Read(protector, "tampered" + protectedQuote, 7) == null, "Tampered quote accepted.");
Check(!checkoutQuote.MatchesLines(new[] { new SellerQuoteLine(1, 2, 101m), new SellerQuoteLine(2, 2, 95m) }), "Changed basket accepted.");
Check(!(checkoutQuote with { Lines = new[] { new SellerQuoteLine(1, 1, 101m), new SellerQuoteLine(1, 2, 95m) } }).IsValid(), "Duplicate listing accepted.");
Console.WriteLine("Seller checkout quotation checks passed.");

var reference = Guid.NewGuid();
var tx = new MF.Models.TransactionModel { TransactionStatus = "Succss", PaymentId = "test-payment", Currency = "KD",
    PaidCurrency = "KD", TransationValue = "291.000" };
var evidence = new MF.Models.GenericResponse<MF.Models.GetPaymentStatusResponse> { IsSuccess = true,
    Data = new MF.Models.GetPaymentStatusResponse { InvoiceId = 12345, InvoiceStatus = "Paid",
        CustomerReference = reference.ToString("N"), InvoiceValue = 291m, InvoiceTransactions = new() { tx } } };
string? Verified() => MarketplacePaymentEvidence.VerifiedPaymentId(evidence,12345,reference,291m);
Check(Verified()=="test-payment","Valid sandbox evidence rejected.");
evidence.Data.InvoiceStatus="Pending";
Check(Verified()==null,"Pending invoice accepted.");
evidence.Data.InvoiceStatus="Paid";
tx.TransactionStatus="Failed";
Check(Verified()==null,"Failed transaction accepted.");
tx.TransactionStatus="Succss";
tx.Currency="USD";
Check(Verified()==null,"Wrong currency accepted.");
tx.Currency="KD";
tx.TransationValue="290.000";
Check(Verified()==null,"Wrong transaction amount accepted.");
tx.TransationValue="291.000";
Check(MarketplacePaymentEvidence.VerifiedPaymentId(evidence,12346,reference,291m)==null,"Wrong invoice accepted.");
Check(MarketplacePaymentEvidence.VerifiedPaymentId(evidence,12345,Guid.NewGuid(),291m)==null,"Wrong order reference accepted.");
evidence.Data.InvoiceTransactions.Add(tx);
Check(Verified()==null,"Ambiguous multiple successful transactions accepted.");
Console.WriteLine("Marketplace payment evidence checks passed.");


// Configurable accent colors must be safe CSS values and readable on light or dark backgrounds.
Check(StoreTheme.From(null).Primary == StoreTheme.DefaultColor, "Missing theme fallback incorrect.");
Check(StoreTheme.From("#abc").Primary == StoreTheme.DefaultColor, "Invalid theme fallback incorrect.");
Check(!StoreTheme.IsValid("#123456; color:red"), "CSS injection accepted.");
Check(StoreTheme.From("#ffffff").Foreground == "#000000", "Light accent text incorrect.");
Check(StoreTheme.From("#000000").Foreground == "#FFFFFF", "Dark accent text incorrect.");
Check(StoreTheme.From("#16e2f5").Primary == "#16E2F5", "Saved accent altered.");
double ThemeLuminance(string color)
{
    double Channel(int index)
    {
        double n = Convert.ToInt32(color.Substring(index, 2), 16) / 255d;
        return n <= 0.04045 ? n / 12.92 : Math.Pow((n + 0.055) / 1.055, 2.4);
    }
    return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
}
double ThemeContrast(string a, string b)
{
    double x = ThemeLuminance(a), y = ThemeLuminance(b);
    return (Math.Max(x,y) + 0.05) / (Math.Min(x,y) + 0.05);
}
foreach (var color in new[] { "#FFFFFF", "#000000", "#16E2F5", "#1569C7", "#F3E5AB", "#E3735E", "#829F82", "#075A96", "#767676" })
{
    var theme = StoreTheme.From(color);
    Check(ThemeContrast(theme.Primary,theme.Foreground) >= 4.5, "Theme foreground contrast insufficient: " + color);
    Check(ThemeContrast(theme.Hover,theme.HoverForeground) >= 4.5, "Theme hover contrast insufficient: " + color);
    Check(ThemeContrast(theme.Link,"#FFFFFF") >= 4.5, "Theme link contrast insufficient: " + color);
    Check(ThemeContrast(theme.Link,"#F5F5F5") >= 4.5, "Theme neutral surface contrast insufficient: " + color);
    Check(ThemeContrast(theme.Link,theme.Tint) >= 4.5, "Theme selected surface contrast insufficient: " + color);
}
Console.WriteLine("Configurable theme checks passed.");


var productPage = new EcommerceWebApplication.Models.PageMaster { PageId = 101, PageNameEng = "Products" };
var ordersPage = new EcommerceWebApplication.Models.PageMaster { PageId = 102, PageNameEng = "Orders" };
var productRights = new EcommerceWebApplication.Models.RolePermission { PageId = 101, ListPage = true, UpdatePage = true };
var orderRights = new EcommerceWebApplication.Models.RolePermission { PageId = 102, ListPage = true, ReportPrint = true };
var productRow = EcommerceWebApplication.Models.ViewModels.PermissionRow.From(productPage, productRights, false);
var orderRow = EcommerceWebApplication.Models.ViewModels.PermissionRow.From(ordersPage, orderRights, false);
Check(productRow.UpdatePage && !orderRow.UpdatePage && !productRow.ReportPrint && orderRow.ReportPrint,
    "Page-specific permission flags were merged.");
var permissionForm = new EcommerceWebApplication.Models.ViewModels.RolePermissionForm { RoleId = 1, Rows = new() { productRow, orderRow } };
Check(permissionForm.HasExactPages(new[] { 101, 102 }), "Valid page matrix rejected.");
Check(!permissionForm.HasExactPages(new[] { 101, 102, 103 }), "Incomplete matrix accepted.");
permissionForm.Rows[1] = productRow;
Check(!permissionForm.HasExactPages(new[] { 101, 102 }), "Duplicate page accepted.");
permissionForm.Rows[1] = orderRow;
Check(!permissionForm.HasExactPages(new[] { 101, 103 }), "Unknown page accepted.");
orderRow.ReportPrint = false;
orderRow.ApplyTo(orderRights);
Check(productRights.UpdatePage && orderRights.ListPage && !orderRights.UpdatePage && !orderRights.ReportPrint,
    "Editing one page changed unrelated permissions.");
orderRow.ListPage = false;
Check(!orderRow.HasRights, "Empty permission row retained rights.");
Console.WriteLine("Page permission matrix checks passed.");

Check(EcommerceWebApplication.Models.VendorPermissionPages.ScopeFor("VendorStaff") == "Vendor" &&
    EcommerceWebApplication.Models.VendorPermissionPages.ScopeFor("Admin") == "Admin", "Portal scope mismatch.");
var vendorGrant = new EcommerceWebApplication.Models.ViewModels.PermissionRow { ListPage = true, UpdatePage = true };
Check(vendorGrant.IsValidVendorGrant("vendor.stock", "VendorManager"), "Valid stock grant rejected.");
Check(!vendorGrant.IsValidVendorGrant("vendor.dashboard", "VendorManager"), "Dashboard update allowed.");
vendorGrant.UpdatePage = false;
Check(!vendorGrant.IsValidVendorGrant("vendor.finance", "VendorManager"), "Manager finance grant allowed.");
Check(vendorGrant.IsValidVendorGrant("vendor.finance", "VendorOwner"), "Owner finance grant rejected.");
vendorGrant.ListPage = false;
vendorGrant.CreatePage = true;
Check(!vendorGrant.IsValidVendorGrant("vendor.listings", "VendorStaff"), "Write without view allowed.");
Console.WriteLine("Vendor permission scope checks passed.");
