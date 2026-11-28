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
        method == "GET" && (
            controller == typeof(AccountController) && action is "Logout" or "AccessDenied" ||
            controller == typeof(HomeController) && action == "SetLanguage");
}
