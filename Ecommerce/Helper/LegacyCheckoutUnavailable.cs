using Microsoft.AspNetCore.Mvc;

namespace EcommerceWebApplication.Helper;

// Legacy payment routes cannot create orders or mutate financial state until a verified replacement exists.
public static class LegacyCheckoutUnavailable
{
    public static ObjectResult Result() => new(new
    {
        success = false,
        code = "LEGACY_CHECKOUT_UNAVAILABLE",
        message = "This checkout route is temporarily unavailable."
    }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
}
