using EcommerceWebApplication.Controllers;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using MF.Models;
using MF.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    // ✅ Add MVC with ViewLocalization
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowReactApp",
            policy =>
            {
                policy.WithOrigins("http://localhost:3000",  // React dev server
                    "https://www.shopsabahiyacoop.com",   // Production with www
                    "https://shopsabahiyacoop.com"       // Production without www
                    )
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
    });
    //builder.Services.AddCors(options =>
    //{
    //    options.AddPolicy("AllowReactApp",
    //        policy =>
    //        {
    //            policy.WithOrigins(
    //                "http://localhost:3000",          
    //                "https://www.shopsabahiyacoop.com",
    //                "https://shopsabahiyacoop.com",
    //                "https://192.168.29.100:8092"     
    //            )
    //            .AllowAnyHeader()
    //            .AllowAnyMethod()
    //            .AllowCredentials(); // optional (if cookies/token)
    //        });
    //});

    builder.Services.AddLocalization(options => options.ResourcesPath = "Resource");
    builder.Services.AddControllersWithViews()
        .AddViewLocalization()
        .AddDataAnnotationsLocalization();
    builder.Services.AddDbContext<EcommerceDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    // Register ApiSettings from appsettings.json
    builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
    builder.Services.AddScoped<IEmailService, EmailService>();
    // Register HttpClient for UserController

    builder.Services.AddScoped<IPaymentService, PaymentService>();
    builder.Services.AddScoped<IMFClient, MFClient>();
    builder.Services.AddScoped<VendorPortalAccess>();
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("CustomerAuth", context =>
            RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        options.AddPolicy(VendorPortalAuth.LoginRatePolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    });
    //builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    //    .AddCookie(options =>
    //    {
    //        options.Cookie.HttpOnly = true;
    //        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
    //        options.LoginPath = "/Account/Login";
    //        options.AccessDeniedPath = "/Account/AccessDenied";
    //        options.SlidingExpiration = true;
    //        options.ExpireTimeSpan = TimeSpan.FromHours(3);

    //    });

    //builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    //.AddCookie(options =>
    //{
    //    options.Cookie.HttpOnly = true;
    //    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    //    options.Cookie.Name = "EcommerceAuthCookie";   // ✅ Custom cookie name

    //    options.LoginPath = "/Account/Login";
    //    options.AccessDeniedPath = "/Account/AccessDenied";

    //    options.SlidingExpiration = true;              // ✅ Auto extend expiry
    //    options.ExpireTimeSpan = TimeSpan.FromDays(30); // ✅ 30 days login

    //    options.Cookie.SameSite = SameSiteMode.Lax;    // ✅ Important
    //    options.Cookie.IsEssential = true;             // ✅ Required for production
    //});
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.Name = "EcommerceAuthCookie";

        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.SlidingExpiration = false; // ❗ Important
        options.ExpireTimeSpan = TimeSpan.FromDays(365); 

        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            if (context.Principal?.IsInRole("Admin") != true) return;
            var principal = context.Principal;
            var email = principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var db = context.HttpContext.RequestServices.GetRequiredService<EcommerceDbContext>();
            if (!int.TryParse(principal.FindFirst("UserId")?.Value, out var id) ||
                !await db.Users.AnyAsync(u => u.UserId == id && u.Email == email && u.IsActive &&
                    u.Role != null && u.Role.IsActive && u.Role.RoleName == "Admin", context.HttpContext.RequestAborted))
                context.RejectPrincipal();
        };
    })
    .AddBearerToken(CustomerApiAuth.Scheme, options => options.BearerTokenExpiration = TimeSpan.FromHours(3))
    .AddCookie(VendorPortalAuth.Scheme, options =>
    {
        VendorPortalAuth.ConfigureCookie(options);
        options.Events.OnValidatePrincipal = async context =>
        {
            var access = context.HttpContext.RequestServices.GetRequiredService<VendorPortalAccess>();
            if (!await access.IsCurrentAsync(context.Principal, context.HttpContext.RequestAborted))
                context.RejectPrincipal();
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(VendorPortalAuth.Policy, policy =>
        {
            policy.AddAuthenticationSchemes(VendorPortalAuth.Scheme);
            policy.RequireAuthenticatedUser();
            policy.RequireRole(VendorRoles.Owner, VendorRoles.Manager, VendorRoles.Staff);
        });
        options.AddPolicy("SuperAdminOnly", policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole("Admin") &&
                context.User.HasClaim(c =>
                    c.Type == System.Security.Claims.ClaimTypes.Email &&
                    c.Value.Equals("admin@gmail.com", StringComparison.OrdinalIgnoreCase)
                )
            )
        );
    });

    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromDays(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });


    var app = builder.Build();
    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }
    //app.UseCors("AllowReactApp");
    //app.UsePathBase("/Admin");
    //app.Use(async (context, next) =>
    //{
    //    context.Request.PathBase = "/admin";
    //    await next.Invoke();
    //});
    //using (var scope = app.Services.CreateScope())
    //{
    //    var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
    //    db.Database.Migrate(); // ✅ Apply migrations automatically

    //    // ✅ Ensure Admin Role exists
    //    var adminRole = db.Roles.FirstOrDefault(r => r.RoleName == "Admin");
    //    if (adminRole == null)
    //    {
    //        adminRole = new Role
    //        {
    //            RoleName = "Admin",
    //            RoleNameArb = "مسؤول",
    //            IsActive = true,
    //            CreatedAt = DateTime.UtcNow
    //        };
    //        db.Roles.Add(adminRole);
    //        db.SaveChanges();
    //    }

    //    // ✅ Ensure Admin User exists
    //    if (!db.Users.Any(u => u.Email == "admin@ecommerce.com"))
    //    {
    //        var adminUser = new User
    //        {
    //            FullName = "Super Admin",
    //            FullNameArb = "المسؤول الأعلى",
    //            Email = "admin@ecommerce.com",
    //            Phone = "0000000000",
    //            PasswordHash = "Admin@123",  // ✅ Plain password
    //            RoleId = adminRole.RoleId,
    //            IsActive = true,
    //            CreatedAt = DateTime.UtcNow
    //        };
    //        db.Users.Add(adminUser);
    //        db.SaveChanges();
    //    }
    //}

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.Use(async (context, next) =>
{
    // Disable caching for every response
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";
    await next();
});

    var supportedCultures = new[] { "en", "ar" };
    var localizationOptions = new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture("en"),
        SupportedCultures = supportedCultures.Select(c => new CultureInfo(c)).ToList(),
        SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList()
    };

    localizationOptions.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());





    app.UseRequestLocalization(localizationOptions);
    app.UseRouting();
    app.UseRateLimiter();
    app.UseSession();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
    name: "adminLogin",
    pattern: "admin",
    defaults: new { controller = "Account", action = "Login" }
);


    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=WebHome}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred: {ex.Message}");

    string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
    Directory.CreateDirectory(logDirectory); // Ensure Logs folder exists

    string logFilePath = Path.Combine(logDirectory, "errorlog.txt");

    File.AppendAllText(logFilePath, $@"
        =========== ERROR ===========
        DateTime      : {DateTime.Now}
        Message       : {ex.Message}
        StackTrace    : {ex.StackTrace}
        InnerException: {ex.InnerException?.Message}
        =============================
        ");

}

