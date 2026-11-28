using System.Reflection;
using EcommerceWebApplication.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void RequireController(Type controller, string? role = null, string? policy = null)
{
    var auth = controller.GetCustomAttributes<AuthorizeAttribute>(true);
    Check(auth.Any(a => role != null ? a.Roles == role : a.Policy == policy),
        $"{controller.Name}: expected authorization boundary missing.");
    Check(!controller.IsDefined(typeof(AllowAnonymousAttribute), true),
        $"{controller.Name}: controller allows anonymous access.");
    foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        if (method.IsSpecialName || method.IsDefined(typeof(NonActionAttribute), true)) continue;
        Check(!method.IsDefined(typeof(AllowAnonymousAttribute), true),
            $"{controller.Name}.{method.Name}: action bypasses authorization.");
    }
}

Type[] adminControllers =
{
    typeof(AdminDashboardController), typeof(ManageOrdersController), typeof(ManageCustomerController),
    typeof(EcommerceWebApplication.Controllers.Admin.ProductStockController), typeof(ProductsController), typeof(CategoryController),
    typeof(SubCategoryController), typeof(BrandController), typeof(WekendDealsController),
    typeof(CouponController), typeof(BannerController), typeof(SendNotificationController),
    typeof(BlogController), typeof(CompanydetailsController), typeof(AreasController),
    typeof(BlocksController), typeof(ContactController), typeof(AboutUsController), typeof(ReportController)
};
foreach (var controller in adminControllers) RequireController(controller, role: new[] { typeof(BrandController), typeof(CategoryController), typeof(SubCategoryController), typeof(ProductsController) }.Contains(controller) ? "Admin,AdminManager,AdminStaff" : "Admin");

Type[] superAdminControllers =
{
    typeof(UserController), typeof(UserApiController), typeof(RoleApiController),
    typeof(RolePermissionController), typeof(VendorController)
};
foreach (var controller in superAdminControllers) RequireController(controller, policy: "SuperAdminOnly");

foreach (var action in new[] { "Index", "SaveUpdate" })
{
    var method = typeof(ChangecolorsController).GetMethods().Single(m => m.Name == action);
    Check(method.GetCustomAttributes<AuthorizeAttribute>(true).Any(a => a.Roles == "Admin") &&
        !method.IsDefined(typeof(AllowAnonymousAttribute), true), $"Color settings {action} is not admin-only.");
}

var vendor = typeof(VendorPortalController);
Check(vendor.GetCustomAttributes<AuthorizeAttribute>(true).Any(a => a.Policy == "VendorPortalOnly"),
    "Vendor portal policy missing.");
foreach (var method in vendor.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
{
    if (method.IsSpecialName || method.IsDefined(typeof(NonActionAttribute), true) || method.Name is "Login" or "Denied") continue;
    Check(!method.IsDefined(typeof(AllowAnonymousAttribute), true),
        $"Vendor {method.Name} bypasses authorization.");
}
var denied = vendor.GetMethod("Denied")!;
Check(denied.GetParameters().Length == 0 && denied.IsDefined(typeof(HttpGetAttribute)) &&
    denied.IsDefined(typeof(AllowAnonymousAttribute)), "Vendor denied route must remain a public error response.");
RequireController(typeof(UserProfileController), role: "Customer");
Console.WriteLine("Portal authorization metadata regression checks passed.");

RequireController(typeof(SmsController), role: "Admin");
var customerApi = typeof(EcommerceApiController);
foreach (var action in new[] { "GetProfile", "UpdateProfile", "ChangePassword", "UploadProfilePic", "GetUserAddresses", "SaveAddress", "DeleteAddress", "GetUserOrders", "GetOrderInvoice", "GetUserReturnRequests", "RequestReturnReplace", "AddProductReview", "CancelOrder", "SoftDeleteUser", "GetOrderDetails" })
{
    var method = customerApi.GetMethods().Single(m => m.Name == action);
    Check(method.GetCustomAttributes<AuthorizeAttribute>().Any(a =>
        a.AuthenticationSchemes == "CustomerApi" && a.Roles == "Customer"), $"Missing customer API authorization: {action}");
}
foreach (var action in new[] { "GetNotificationCount", "GetNewOrders", "MarkNotificationsAsSeen" })
{
    var method = customerApi.GetMethods().Single(m => m.Name == action);
    Check(method.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Roles == "Admin"), $"Missing admin notification authorization: {action}");
}
var markSeen = customerApi.GetMethod("MarkNotificationsAsSeen")!;
Check(markSeen.IsDefined(typeof(HttpPostAttribute)) && markSeen.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)),
    "Notification mutation must require POST and antiforgery.");
var ticket = EcommerceWebApplication.Helper.CustomerApiAuth.CreateTicket(41, "test-credential", DateTimeOffset.UtcNow, TimeSpan.FromHours(3));
Check(EcommerceWebApplication.Helper.CustomerApiAuth.TryGetUserId(ticket.Principal, out int customerId) && customerId == 41, "Customer identity missing.");
Check(EcommerceWebApplication.Helper.CustomerApiAuth.IsCurrent(ticket.Principal, "test-credential") &&
    !EcommerceWebApplication.Helper.CustomerApiAuth.IsCurrent(ticket.Principal, "changed"), "Customer token credential invalidation failed.");
Check(!EcommerceWebApplication.Helper.CustomerApiAuth.TryGetUserId(new System.Security.Claims.ClaimsPrincipal(), out _), "Anonymous identity accepted.");
Console.WriteLine("Customer API and notification authorization metadata checks passed.");

foreach (var action in new[] { "GetCart", "SaveCart", "UpdateCartQuantity", "DeleteCartItem", "ClearCart", "ApplyCoupon" })
{
    var method = customerApi.GetMethods().Single(m => m.Name == action);
    Check(method.GetCustomAttributes<AuthorizeAttribute>().Any(a =>
        a.AuthenticationSchemes == "CustomerApi" && a.Roles == "Customer"), $"Cart authorization missing: {action}");
}
static void CheckUnavailable(IActionResult result) =>
    Check(result is ObjectResult { StatusCode: 503 }, "Unsafe legacy payment route was not blocked.");
var blockedApi = new EcommerceApiController(null!, null!, null!, null!);
CheckUnavailable(blockedApi.PlaceOrder(null!));
CheckUnavailable(blockedApi.GuestRegister(null!));
CheckUnavailable(blockedApi.UpdateStatus(null!));
CheckUnavailable(blockedApi.ErrorUpdateStatus(null!));
var blockedPayment = new PaymentApiController();
CheckUnavailable(blockedPayment.SendPayment(null!));
CheckUnavailable(blockedPayment.ExecutePayment(null!));
CheckUnavailable(blockedPayment.VerifyPayment("test"));
CheckUnavailable(blockedPayment.GetPaymentStatus(null!));
CheckUnavailable(blockedPayment.Session());
CheckUnavailable(blockedPayment.paymentsuccess());
CheckUnavailable(blockedPayment.PaymentFailed());
Console.WriteLine("Legacy financial endpoint containment checks passed.");

var customerPassword = EcommerceWebApplication.Helper.CustomerPassword.Hash("Sample-Password-42");
Check(customerPassword != "Sample-Password-42" &&
    EcommerceWebApplication.Helper.CustomerPassword.Verify(customerPassword, "Sample-Password-42", out _) &&
    !EcommerceWebApplication.Helper.CustomerPassword.Verify(customerPassword, "wrong", out _), "Customer password hashing failed.");
Check(EcommerceWebApplication.Helper.CustomerPassword.Verify("legacy-password", "legacy-password", out var upgradeCustomer) && upgradeCustomer,
    "Legacy credential upgrade failed.");
Check(!EcommerceWebApplication.Helper.CustomerPassword.Verify("customer-v1:invalid", "customer-v1:invalid", out _),
    "Malformed hash accepted as plaintext.");
Check(EcommerceWebApplication.Helper.CustomerPassword.ApiOtpKey("Example@example.com", true) !=
    EcommerceWebApplication.Helper.CustomerPassword.ApiOtpKey("Example@example.com", false), "OTP purposes overlap.");
Console.WriteLine("Customer password and OTP purpose checks passed.");

var webhook = typeof(MarketplaceWebhookController).GetMethod("Receive")!;
Check(webhook.IsDefined(typeof(HttpPostAttribute)) && webhook.IsDefined(typeof(AllowAnonymousAttribute)) &&
    webhook.IsDefined(typeof(IgnoreAntiforgeryTokenAttribute)), "Webhook endpoint metadata incorrect.");
const string signedBody = """
    {"Event":{"Code":1,"Name":"PAYMENT_STATUS_CHANGED"},"Data":{"Invoice":{"Id":"6409988","Status":"PAID","ExternalIdentifier":null},"Transaction":{"Status":"SUCCESS","PaymentId":"07076409988323998875"}}}
    """;
const string testSecret = "test-webhook-secret";
const string testSignature = "ATzf+aw3aQq7YxODV8ROn5DMXqCFIQ/vKj3RgrAUvgE=";
static EcommerceWebApplication.Helper.MarketplaceWebhookSignature.PaymentEvent? CheckSignature(string json, string? signature, string? secret)
{
    using var document = System.Text.Json.JsonDocument.Parse(json);
    return EcommerceWebApplication.Helper.MarketplaceWebhookSignature.Verify(document.RootElement, signature, secret);
}
var paymentEvent = CheckSignature(signedBody, testSignature, testSecret);
Check(paymentEvent is { InvoiceId: 6409988, IsPaid: true, PaymentId: "07076409988323998875" }, "Valid signature rejected.");
Check(CheckSignature(signedBody, testSignature, "wrong-secret") == null &&
    CheckSignature(signedBody, null, testSecret) == null &&
    CheckSignature(signedBody, "invalid", testSecret) == null, "Invalid signature accepted.");
Check(CheckSignature(signedBody.Replace("6409988","6409989"), testSignature, testSecret) == null &&
    CheckSignature(signedBody.Replace("SUCCESS","FAILED"), testSignature, testSecret) == null &&
    CheckSignature(signedBody.Replace("07076409988323998875","07076409988323998876"), testSignature, testSecret) == null &&
    CheckSignature(signedBody.Replace("null","\"changed\""), testSignature, testSecret) == null,
    "Tampered signed payment fields accepted.");
Check(CheckSignature(signedBody.Replace("\"Code\":1","\"Code\":1,\"Code\":1"), testSignature, testSecret) == null &&
    CheckSignature(signedBody.Replace("PAYMENT_STATUS_CHANGED","REFUND_STATUS_CHANGED"), testSignature, testSecret) == null,
    "Ambiguous or unsupported webhook accepted.");
Console.WriteLine("MyFatoorah V2 webhook signature regression checks passed.");

var noView = EcommerceWebApplication.Helper.AdminPageRights.From(false, true, true, true);
Check(!noView.View && !noView.Create && !noView.Edit && !noView.Delete, "Write access without list permission.");
var readOnlyBrand = EcommerceWebApplication.Helper.AdminPageRights.From(true, false, false, false);
Check(readOnlyBrand.View && !readOnlyBrand.Create && !readOnlyBrand.Edit && !readOnlyBrand.Delete, "Read-only brand permission expanded.");
Check(EcommerceWebApplication.Helper.AdminStaffAccess.IsRestricted("AdminManager") &&
    EcommerceWebApplication.Helper.AdminStaffAccess.IsRestricted("AdminStaff") &&
    !EcommerceWebApplication.Helper.AdminStaffAccess.IsRestricted("Admin") &&
    !EcommerceWebApplication.Helper.AdminStaffAccess.IsRestricted("VendorStaff"), "Staff roles overlap.");
Check(EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(BrandController), "Index", "GET") &&
    EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(BrandController), "CreateBrand", "POST") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(BrandController), "Delete", "GET") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(BrandController), "NewAction", "GET") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(VendorController), "Index", "GET") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(EcommerceApiController), "GetProfile", "GET") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(AdminDashboardController), "Index", "GET"),
    "Restricted admin action boundary failed.");
Console.WriteLine("Restricted admin Brand pilot checks passed.");

var allowedCatalogActions = new[] {
    (typeof(CategoryController), "CreateAsync", "POST"), (typeof(CategoryController), "Edit", "GET"),
    (typeof(SubCategoryController), "CreateSubCategoryAsync", "POST"), (typeof(SubCategoryController), "DeleteSubCategory", "POST"),
    (typeof(ProductsController), "ProductList", "GET"), (typeof(ProductsController), "GetSubCategories", "GET"),
    (typeof(ProductsController), "DeleteProductImage", "POST")
};
foreach (var (controller, action, verb) in allowedCatalogActions)
    Check(EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(controller, action, verb),
        $"Catalog action is not classified: {controller.Name}.{action}.");
Check(!EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(CategoryController), "SaveOrUpdateTopCategory", "POST") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(CategoryController), "TopCategory", "GET") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(ProductsController), "DeleteProduct", "GET"),
    "Unclassified catalog action was allowed.");
foreach (var name in new[] { "Create", "Edit", "DeleteProduct", "DeleteProductImage" })
    foreach (var action in typeof(ProductsController).GetMethods().Where(m => m.Name == name && m.IsDefined(typeof(HttpPostAttribute))))
        Check(action.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)), $"Missing Product antiforgery: {name}");
Check(EcommerceWebApplication.Helper.AdminStaffAccess.Pages.Select(p => p.Code).Distinct().Count() == 4,
    "Unexpected catalog permission mapping.");
Console.WriteLine("Catalog restricted-admin action and antiforgery checks passed.");
