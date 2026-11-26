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
