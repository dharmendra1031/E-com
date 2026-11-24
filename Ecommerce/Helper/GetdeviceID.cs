using Microsoft.AspNetCore.Http;

namespace EcommerceWebApplication.Helper
{
    public static class DeviceIdHelper
    {
        public static string GetOrCreateDeviceId(HttpContext context)
        {
            if (!context.Request.Cookies.ContainsKey("DeviceId"))
            {
                string deviceId = Guid.NewGuid().ToString();

                context.Response.Cookies.Append("DeviceId", deviceId, new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(30),
                    HttpOnly = true,
                    IsEssential = true
                });

                return deviceId;
            }

            return context.Request.Cookies["DeviceId"];
        }
    }
}
