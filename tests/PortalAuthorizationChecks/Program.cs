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
foreach (var controller in adminControllers) RequireController(controller, role: controller==typeof(ReportController)?"Admin,AdminManager,AdminStaff,Customer":new[] { typeof(BrandController), typeof(CategoryController), typeof(SubCategoryController), typeof(ProductsController), typeof(ManageOrdersController), typeof(ManageCustomerController), typeof(AreasController), typeof(BlocksController), typeof(BannerController), typeof(BlogController), typeof(CouponController), typeof(WekendDealsController), typeof(SendNotificationController), typeof(CompanydetailsController), typeof(ContactController), typeof(AboutUsController), typeof(ReportController), typeof(AdminDashboardController), typeof(EcommerceWebApplication.Controllers.Admin.ProductStockController) }.Contains(controller) ? "Admin,AdminManager,AdminStaff" : "Admin");

Type[] superAdminControllers =
{
    typeof(UserController), typeof(UserApiController), typeof(RoleApiController),
    typeof(RolePermissionController), typeof(VendorController)
};
foreach (var controller in superAdminControllers) RequireController(controller, policy: "SuperAdminOnly");

foreach (var action in new[] { "Index", "SaveUpdate" })
{
    var method = typeof(ChangecolorsController).GetMethods().Single(m => m.Name == action);
    Check(method.GetCustomAttributes<AuthorizeAttribute>(true).Any(a => a.Roles == "Admin,AdminManager,AdminStaff") &&
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
foreach(var name in new[] { "Inventory", "Returns", "Reports", "StoreProfile" })
{
    var method=vendor.GetMethod(name)!;
    Check(method.IsDefined(typeof(HttpGetAttribute)) && !method.IsDefined(typeof(AllowAnonymousAttribute)),
        "Vendor register must be authenticated and read-only.");
}
var reportToday = new DateTime(2026,11,28);
Check(EcommerceWebApplication.Helper.VendorOrderQueries.ValidReportRange(reportToday.AddDays(-89),reportToday,reportToday) &&
    !EcommerceWebApplication.Helper.VendorOrderQueries.ValidReportRange(reportToday.AddDays(-90),reportToday,reportToday) &&
    !EcommerceWebApplication.Helper.VendorOrderQueries.ValidReportRange(reportToday,reportToday.AddDays(1),reportToday) &&
    !EcommerceWebApplication.Helper.VendorOrderQueries.ValidReportRange(reportToday,reportToday.AddDays(-1),reportToday),
    "Invalid vendor report period accepted.");
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

var evidenceRef = Guid.NewGuid();
var evidenceTransaction = new MF.Models.TransactionModel { TransactionStatus = "Succss", Currency = "KD", PaidCurrency = "KWD", TransationValue = "12.345", PaymentId = "PAY_123" };
var evidenceResponse = new MF.Models.GenericResponse<MF.Models.GetPaymentStatusResponse> { IsSuccess = true, Data = new() { InvoiceId = 12345, CustomerReference = evidenceRef.ToString("N"), InvoiceStatus = "Paid", InvoiceValue = 12.345m, InvoiceTransactions = new() { evidenceTransaction } } };
Check(EcommerceWebApplication.Helper.MarketplacePaymentEvidence.VerifiedPaymentId(evidenceResponse, 12345, evidenceRef, 12.345m) == "PAY_123" &&
    EcommerceWebApplication.Helper.MarketplacePaymentEvidence.VerifiedRecoveryInvoice(evidenceResponse, "PAY_123", evidenceRef, 12.345m) == 12345,
    "Valid payment evidence rejected.");
Check(EcommerceWebApplication.Helper.MarketplacePaymentEvidence.VerifiedPaymentId(evidenceResponse, 12346, evidenceRef, 12.345m) == null &&
    EcommerceWebApplication.Helper.MarketplacePaymentEvidence.VerifiedPaymentId(evidenceResponse, 12345, Guid.NewGuid(), 12.345m) == null &&
    EcommerceWebApplication.Helper.MarketplacePaymentEvidence.VerifiedPaymentId(evidenceResponse, 12345, evidenceRef, 12.346m) == null &&
    EcommerceWebApplication.Helper.MarketplacePaymentEvidence.VerifiedRecoveryInvoice(evidenceResponse, "PAY_456", evidenceRef, 12.345m) == null,
    "Mismatched payment evidence accepted.");
evidenceResponse.Data.InvoiceTransactions.Add(null!);
Check(EcommerceWebApplication.Helper.MarketplacePaymentEvidence.VerifiedPaymentId(evidenceResponse, 12345, evidenceRef, 12.345m) == null, "Malformed transaction list accepted.");
evidenceResponse.Data.InvoiceTransactions.RemoveAt(1);
evidenceTransaction.PaymentId = "invalid payment";
Check(EcommerceWebApplication.Helper.MarketplacePaymentEvidence.VerifiedPaymentId(evidenceResponse, 12345, evidenceRef, 12.345m) == null, "Malformed payment identifier accepted.");
evidenceTransaction.PaymentId = "PAY_123";
evidenceTransaction.PaidCurrency = "USD";
Check(EcommerceWebApplication.Helper.MarketplacePaymentEvidence.VerifiedPaymentId(evidenceResponse, 12345, evidenceRef, 12.345m) == null, "Wrong paid currency accepted.");
Console.WriteLine("Marketplace payment evidence regression checks passed.");

var addressFixture = new EcommerceWebApplication.Models.UserAddress { AddressId = 7, Area = "1", Block = "2", Street = "Original street", HouseNo = "12", Phone = "12345678" };
var areaFixture = new EcommerceWebApplication.Models.Area { Id = 1, AreaName = "Original area", AreaNameArb = "المنطقة" };
var blockFixture = new EcommerceWebApplication.Models.Block { Id = 2, AreaId = 1, BlockName = "Block 2" };
var addressJson = EcommerceWebApplication.Helper.SellerDeliverySnapshot.Capture(addressFixture, areaFixture, blockFixture).ToJson();
addressFixture.Street = "Changed street"; areaFixture.AreaName = "Renamed area";
var addressSnapshot = EcommerceWebApplication.Helper.SellerDeliverySnapshot.Read(addressJson);
Check(addressSnapshot is { AddressId: 7, Street: "Original street", AreaName: "Original area", AreaNameArb: "المنطقة" } &&
    EcommerceWebApplication.Helper.SellerDeliverySnapshot.Read(null) == null &&
    EcommerceWebApplication.Helper.SellerDeliverySnapshot.Read("invalid") == null &&
    EcommerceWebApplication.Helper.SellerDeliverySnapshot.Read("{}") == null, "Historical delivery snapshot failed.");
var zeroQuote = new EcommerceWebApplication.Helper.SellerCheckoutQuote(Guid.NewGuid(), 1, DateTime.UtcNow.AddMinutes(10), 0, 0,
    new[] { new EcommerceWebApplication.Helper.SellerQuoteLine(1,1,0) });
Check(!zeroQuote.IsValid() && (zeroQuote with { DeliveryCharge = 1 }).IsValid() &&
    !(zeroQuote with { Lines = new EcommerceWebApplication.Helper.SellerQuoteLine[] { null! } }).IsValid(),
    "Invalid or unpayable quote accepted.");
Console.WriteLine("Delivery snapshot and payable quote regression checks passed.");

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
    EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(AdminDashboardController), "Index", "GET"),
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
Check(EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(CategoryController), "SaveOrUpdateTopCategory", "POST") &&
    EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(CategoryController), "TopCategory", "GET") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(ProductsController), "DeleteProduct", "GET"),
    "Unclassified catalog action was allowed.");
foreach (var name in new[] { "Create", "Edit", "DeleteProduct", "DeleteProductImage" })
    foreach (var action in typeof(ProductsController).GetMethods().Where(m => m.Name == name && m.IsDefined(typeof(HttpPostAttribute))))
        Check(action.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)), $"Missing Product antiforgery: {name}");
Check(EcommerceWebApplication.Helper.AdminStaffAccess.Pages.Select(p => p.Code).Distinct().Count() == 21,
    "Unexpected catalog permission mapping.");
Console.WriteLine("Catalog restricted-admin action and antiforgery checks passed.");

var stockController = typeof(EcommerceWebApplication.Controllers.Admin.ProductStockController);
Check(EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(stockController, "Index", "GET") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(stockController, "AddStock", "POST") &&
    EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(ManageOrdersController), "GetOrderDetails", "GET") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(ManageOrdersController), "UpdateOrderStatus", "POST") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(ManageOrdersController), "UpdatePaymentStatus", "POST") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(ManageOrdersController), "GetInvoice", "GET"),
    "Restricted legacy stock or order write boundary failed.");
foreach (var name in new[] { "SaveOrUpdateTopCategory", "DeleteTopCategory" })
    Check(typeof(CategoryController).GetMethod(name)!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)),
        $"Missing Top Category antiforgery: {name}");
Console.WriteLine("Top Category and read-only stock/order permission checks passed.");

Check(EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(stockController,"MarketplaceStock","GET") &&
    EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(stockController,"MarketplaceMovements","GET") &&
    EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(stockController,"MarketplaceMovements","POST") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(stockController,"AddStock","POST"),
    "Marketplace stock boundary failed.");
Check(stockController.GetMethods().Single(m=>m.Name=="MarketplaceMovements" && m.IsDefined(typeof(HttpPostAttribute)))
    .IsDefined(typeof(ValidateAntiForgeryTokenAttribute)), "Marketplace adjustment missing antiforgery.");
Console.WriteLine("Marketplace admin inventory endpoint checks passed.");

Check(EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(ManageOrdersController),"MarketplaceOrders","GET") &&
    EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(ManageOrdersController),"MarketplaceOrderDetails","GET") &&
    EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(ManageOrdersController),"AdvanceMarketplaceFulfillment","POST") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(ManageOrdersController),"AdvanceMarketplaceFulfillment","GET") &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(ManageOrdersController),"UpdatePaymentStatus","POST"),
    "Marketplace order action boundary failed.");
Check(typeof(ManageOrdersController).GetMethod("AdvanceMarketplaceFulfillment")!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)),
    "Marketplace fulfillment missing antiforgery.");
Check(EcommerceWebApplication.Helper.VendorOrderQueries.CanAdvance("AwaitingPacking","Packed") &&
    EcommerceWebApplication.Helper.VendorOrderQueries.CanAdvance("Packed","HandedOver") &&
    EcommerceWebApplication.Helper.VendorOrderQueries.CanAdvance("HandedOver","Delivered",true,true) &&
    !EcommerceWebApplication.Helper.VendorOrderQueries.CanAdvance("HandedOver","Delivered") &&
    !EcommerceWebApplication.Helper.VendorOrderQueries.CanAdvance("HandedOver","Delivered",true,false) &&
    !EcommerceWebApplication.Helper.VendorOrderQueries.CanAdvance("AwaitingPacking","Delivered",true,true),
    "Fulfillment transitions are incorrect.");
Console.WriteLine("Admin marketplace order authorization and transition checks passed.");

var refundRef=Guid.NewGuid();
var refundRow=new EcommerceWebApplication.Helper.MarketplaceRefundGateway.RefundEntry {
    RefundId=99,InvoiceId=12345,ExternalIdentifier=refundRef.ToString("N"),RefundStatus="Refunded",
    BaseCurrency="KWD",Amount=10,RefundAmount=10
};
var refundResponse=new MF.Models.GenericResponse<EcommerceWebApplication.Helper.MarketplaceRefundGateway.RefundData> {
    IsSuccess=true,Data=new() { RefundStatusResult=new() { refundRow } }
};
Check(EcommerceWebApplication.Helper.MarketplaceRefundGateway.VerifiedRefundId(refundResponse,12345,refundRef,10)==99 &&
    EcommerceWebApplication.Helper.MarketplaceRefundGateway.VerifiedRefundId(refundResponse,12346,refundRef,10)==null &&
    EcommerceWebApplication.Helper.MarketplaceRefundGateway.VerifiedRefundId(refundResponse,12345,Guid.NewGuid(),10)==null &&
    EcommerceWebApplication.Helper.MarketplaceRefundGateway.VerifiedRefundId(refundResponse,12345,refundRef,11)==null,
    "Refund evidence correlation failed.");
refundRow.RefundStatus="Pending";
Check(EcommerceWebApplication.Helper.MarketplaceRefundGateway.VerifiedRefundId(refundResponse,12345,refundRef,10)==null,
    "Pending refund accepted.");
refundRow.RefundStatus="Refunded";refundRow.RefundAmount=9;
Check(EcommerceWebApplication.Helper.MarketplaceRefundGateway.VerifiedRefundId(refundResponse,12345,refundRef,10)==null,
    "Incorrect actual refund accepted.");
refundRow.RefundAmount=10;refundRow.BaseCurrency="USD";
Check(EcommerceWebApplication.Helper.MarketplaceRefundGateway.VerifiedRefundId(refundResponse,12345,refundRef,10)==null,
    "Incorrect refund currency accepted.");
foreach(var action in new[]{"StartSandboxRefund","VerifySandboxRefund"})
    Check(typeof(VendorController).GetMethod(action)!.IsDefined(typeof(HttpPostAttribute)) &&
        typeof(VendorController).GetMethod(action)!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)),
        "Refund endpoint missing POST/CSRF protection.");
Console.WriteLine("Sandbox refund verification checks passed.");

Check(typeof(VendorController).GetMethod("PostSettlementPreview")!.IsDefined(typeof(HttpPostAttribute)) &&
    typeof(VendorController).GetMethod("PostSettlementPreview")!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)) &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(VendorController),"PostSettlementPreview","POST"),
    "Settlement preview endpoint authorization boundary failed.");
Console.WriteLine("Settlement preview authorization checks passed.");

Check(typeof(VendorController).GetMethod("ReviewSettlementPreview")!.IsDefined(typeof(HttpPostAttribute)) &&
    typeof(VendorController).GetMethod("ReviewSettlementPreview")!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)) &&
    !EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(typeof(VendorController),"ReviewSettlementPreview","POST"),
    "Settlement approval endpoint authorization boundary failed.");

foreach (var (controller,action,verb) in new[] {
    (typeof(ManageCustomerController),"CustomerList","GET"),(typeof(ManageCustomerController),"GetCustomerDetails","GET"),
    (typeof(ManageCustomerController),"GetCustomerOrderItems","GET"),(typeof(AreasController),"Create","POST"),
    (typeof(AreasController),"Edit","POST"),(typeof(BlocksController),"Create","POST"),(typeof(BlocksController),"Edit","POST") })
    Check(EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(controller,action,verb),"New admin page boundary missing.");
foreach(var (controller,action) in new[] { (typeof(ManageCustomerController),"ChangePassword"),(typeof(ManageCustomerController),"ToggleUserStatus"),
    (typeof(AreasController),"DeleteArea"),(typeof(BlocksController),"DeleteBlock") })
{
    Check(!EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(controller,action,"POST"),"Privileged modification exposed.");
    var method=controller.GetMethod(action)!;
    Check(method.GetCustomAttributes<AuthorizeAttribute>().Any(a=>a.Roles=="Admin") &&
        method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)),"Privileged modification protection missing.");
}

foreach(var (controller,action,verb) in new[]{
    (typeof(BannerController),"AddBanner","GET"),(typeof(BannerController),"AddBanner","POST"),
    (typeof(BannerController),"EditBanner","POST"),(typeof(BlogController),"BlogList","GET"),
    (typeof(BlogController),"CreateOrUpdate","POST"),(typeof(BlogController),"Delete","POST"),
    (typeof(CouponController),"Index","GET"),(typeof(WekendDealsController),"WekendDealsList","GET"),
    (typeof(WekendDealsController),"AutoCompletedeal","GET")})
    Check(EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(controller,action,verb),"Marketing permission route missing.");
foreach(var controller in new[]{typeof(BannerController),typeof(BlogController),typeof(CouponController),typeof(WekendDealsController)})
    foreach(var method in controller.GetMethods().Where(m=>m.DeclaringType==controller && m.IsDefined(typeof(HttpPostAttribute))))
        Check(method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)), "Marketing POST missing CSRF.");
foreach(var controller in new[]{typeof(CouponController),typeof(WekendDealsController)})
    foreach(var method in controller.GetMethods().Where(m=>m.DeclaringType==controller && m.IsDefined(typeof(HttpPostAttribute))))
        Check(!EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(controller,method.Name,"POST") &&
            method.GetCustomAttributes<AuthorizeAttribute>().Any(a=>a.Roles=="Admin"),"Restricted pricing mutation exposed.");

foreach(var (controller,action,verb) in new[]{(typeof(SendNotificationController),"Index","GET"),(typeof(CompanydetailsController),"Index","GET"),(typeof(ContactController),"ContactList","GET"),(typeof(AboutUsController),"UpdateAbout","GET"),(typeof(AboutUsController),"Create","POST"),(typeof(ChangecolorsController),"Index","GET"),(typeof(ChangecolorsController),"SaveUpdate","POST"),(typeof(AdminDashboardController),"Index","GET"),(typeof(ReportController),"ReportPrint","GET"),(typeof(ReportController),"CustomersReport","GET")})
    Check(EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(controller,action,verb),"Remaining admin route missing.");
foreach(var (controller,action,verb) in new[]{(typeof(SendNotificationController),"SaveNotification","POST"),(typeof(CompanydetailsController),"Create","POST"),(typeof(ReportController),"OrderInvoicePrint","GET"),(typeof(ReportController),"ReportPrint","POST")})
    Check(!EcommerceWebApplication.Helper.RestrictedAdminFilter.Allows(controller,action,verb),"Privileged route exposed.");

Check(typeof(WebCheckoutController).GetMethod("PlaceOrder")!.GetCustomAttributes<AuthorizeAttribute>().Any(a=>a.Roles=="Customer"),"Anonymous legacy checkout exposed.");
foreach(var action in new[]{"AddAddress","ChangePassword","DeleteAddress","UpdateProfile"})
    Check(typeof(UserProfileController).GetMethod(action)!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)),"Customer mutation missing CSRF.");
foreach(var action in new[]{"Index","CategoryWiseSalereport","Customers","StockReport","ItemReport","ReportPrint","CategoryReportPrint","CustomersReport","StockReportPrint","ItemfileReport","AutoCompleteSql"})
    Check(typeof(ReportController).GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>().Any(a=>a.Roles=="Admin,AdminManager,AdminStaff"),"Customer report exposure.");
Check(typeof(ReportController).GetMethod("OrderInvoicePrint")!.GetCustomAttributes<AuthorizeAttribute>().Any(a=>a.Roles=="Admin,Customer"),"Legacy invoice authorization missing.");

foreach(var action in new[]{"AddOrUpdateCart","RemoveFromCart"})
    Check(typeof(WebCartController).GetMethod(action)!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)),"Legacy cart mutation missing CSRF.");

if (Environment.GetEnvironmentVariable("MARKETPLACE_TEST_CONNECTION") is { Length: > 0 } marketplaceConnection)
    await VendorDatabaseChecks.RunAsync(marketplaceConnection);
