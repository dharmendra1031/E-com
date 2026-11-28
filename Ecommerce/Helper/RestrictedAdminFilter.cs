using EcommerceWebApplication.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EcommerceWebApplication.Helper;

// The restricted pilot cannot call any unclassified MVC action, even anonymous legacy APIs.
public sealed class RestrictedAdminFilter(AdminStaffAccess access) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!AdminStaffAccess.IsRestricted(context.HttpContext.User)) return Task.CompletedTask;
        if (!access.Enabled || context.ActionDescriptor is not ControllerActionDescriptor action ||
            !Allows(action.ControllerTypeInfo.AsType(), action.MethodInfo.Name, context.HttpContext.Request.Method))
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        return Task.CompletedTask;
    }

    public static bool Allows(Type controller, string action, string method) =>
        controller == typeof(BrandController) && (
            method == "GET" && action is "Index" or "CreateBrand" or "AutoCompleteSubCategory" ||
            method == "POST" && action is "CreateBrand" or "Delete") ||
        controller == typeof(CategoryController) && (
            method == "GET" && action is "Index" or "Create" or "Edit" or "AutoCompleteSql" or "TopCategory" ||
            method == "POST" && action is "CreateAsync" or "Edit" or "DeleteCategory" or "SaveOrUpdateTopCategory" or "DeleteTopCategory") ||
        controller == typeof(SubCategoryController) && (
            method == "GET" && action is "List" or "CreateSubCategory" or "Edit" or "AutoCompleteSubCategory" ||
            method == "POST" && action is "CreateSubCategoryAsync" or "Edit" or "DeleteSubCategory") ||
        controller == typeof(ProductsController) && (
            method == "GET" && action is "ProductList" or "Create" or "Edit" or "GetSubCategories" or "AutoCompleteSql" ||
            method == "POST" && action is "Create" or "Edit" or "DeleteProduct" or "DeleteProductImage") ||
        controller == typeof(EcommerceWebApplication.Controllers.Admin.ProductStockController) &&
            (method == "GET" && action is "Index" or "AutoCompleteSql" or "MarketplaceStock" or "MarketplaceMovements" ||
             method == "POST" && action == "MarketplaceMovements") ||
        controller == typeof(ManageOrdersController) &&
            (method == "GET" && action is "PendingOrder" or "GetOrderDetails" or "MarketplaceOrders" or "MarketplaceOrderDetails" ||
             method == "POST" && action == "AdvanceMarketplaceFulfillment") ||
        controller == typeof(ManageCustomerController) && method == "GET" &&
            action is "CustomerList" or "GetCustomerDetails" or "GetCustomerOrderItems" or "AutoCompleteSql" ||
        controller == typeof(AreasController) && (
            method == "GET" && action is "AreaList" or "Create" or "Edit" or "AutoCompleteArea" ||
            method == "POST" && action is "Create" or "Edit") ||
        controller == typeof(BlocksController) && (
            method == "GET" && action is "BlockList" or "Create" or "Edit" or "AutoCompleteArea" ||
            method == "POST" && action is "Create" or "Edit") ||
        method == "GET" && (
            controller == typeof(AccountController) && action is "Logout" or "AccessDenied" ||
            controller == typeof(HomeController) && action == "SetLanguage");
}
