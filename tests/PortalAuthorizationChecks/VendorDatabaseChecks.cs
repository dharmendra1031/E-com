using System.Security.Claims;
using EcommerceWebApplication.Controllers;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

internal static class VendorDatabaseChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static async Task RunAsync(string connection)
    {
        var builder = new SqlConnectionStringBuilder(connection);
        Check(builder.InitialCatalog == "MarketplaceMigrationTest" &&
            builder.DataSource == "127.0.0.1,1433", "Local disposable marketplace database required.");
        await using var db = new EcommerceDbContext(new DbContextOptionsBuilder<EcommerceDbContext>()
            .UseSqlServer(connection).Options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new[] { "VendorMasterEnabled", "VendorPortalEnabled", "VendorInventoryEnabled", "VendorOrdersEnabled",
                "VendorFulfillmentEnabled", "ReturnWorkflowEnabled", "FinancePreviewEnabled" }
                .ToDictionary(x => "Marketplace:" + x, _ => (string?)"true")).Build();
        var access = new VendorPortalAccess(db, config);
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await db.Vendors.Where(v => v.VendorCode == "A").Select(v => v.VendorId).SingleAsync();
        var b = await db.Vendors.Where(v => v.VendorCode == "B").Select(v => v.VendorId).SingleAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.VendorUsers SET IsActive=CASE WHEN VendorId={b} THEN 1 ELSE 0 END WHERE UserId=1
            """);

        VendorPortalController Portal(int userId, string scheme = VendorPortalAuth.Scheme)
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("UserId", userId.ToString()) }, scheme));
            return new VendorPortalController(db, access, config)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
            };
        }
        var ct = CancellationToken.None;
        var owner = Portal(1);
        Check(await access.CanAsync(b,1,VendorPermissionPages.Finance,ct) &&
            !await access.CanAsync(a,1,VendorPermissionPages.Finance,ct), "Owner membership boundary failed.");
        Check(await owner.Inventory(a,null,1,ct) is NotFoundResult &&
            await owner.Returns(a,1,ct) is NotFoundResult &&
            await owner.Orders(a,ct:ct) is NotFoundResult &&
            await owner.Reports(a,null,null,ct) is NotFoundResult &&
            await owner.StoreProfile(a,ct) is NotFoundResult &&
            await owner.FinanceReview(a,ct:ct) is NotFoundResult, "Cross-vendor controller access accepted.");
        Check(await Portal(1,"Cookies").Inventory(b,null,1,ct) is NotFoundResult,
            "Default cookie identity accepted by vendor controller.");

        var inventory = (await owner.Inventory(b,null,1,ct) as ViewResult)?.Model as MarketplaceStockRegister;
        Check(inventory is { Rows.Count: > 0 } && inventory.Rows.All(x => x.VendorId == b),
            "Scoped inventory query failed.");
        var orders = (await owner.Orders(b,ct:ct) as ViewResult)?.Model as List<VendorOrderRow>;
        Check(orders is { Count: 1 } && orders[0].OrderId == 9 && orders[0].VendorId == b &&
            orders[0].MerchandiseSubtotal == 190m, "Verified order register incorrect.");
        var aOrders = await VendorOrderQueries.ReadPageAsync(db,a,null,1,ct);
        Check(aOrders.Count == 2 && aOrders.All(x=>x.VendorId==a) &&
            await owner.OrderDetails(b,aOrders[0].VendorOrderId,ct) is NotFoundResult,
            "Cross-vendor order detail accepted.");
        var counts = await VendorOrderQueries.ReadCountsAsync(db,b,ct);
        Check(counts.Sum(x=>x.Total)==1 && counts.Single().FulfillmentStatus=="Delivered",
            "Vendor fulfillment counts incorrect.");
        var returns = (await owner.Returns(b,1,ct) as ViewResult)?.Model as List<VendorReturnRow>;
        var aReturns = await VendorOrderQueries.ReadReturnsAsync(db,a,1,ct);
        Check(returns is { Count: 0 } && aReturns.Count>=1, "Vendor returns isolation failed.");
        var reports = (await owner.Reports(b,DateTime.UtcNow.Date.AddDays(-1),DateTime.UtcNow.Date,ct) as ViewResult)?.Model as VendorSalesReport;
        Check(reports != null && reports.Days.Sum(x=>x.OrderCount)==1 && reports.Days.Sum(x=>x.Merchandise)==190m,
            "Vendor report aggregate incorrect.");
        Check(await owner.StoreProfile(b,ct) is ViewResult { Model: VendorPortalStore profile } && profile.VendorId==b,
            "Store profile mismatch.");
        Check(await owner.Reports(b,DateTime.UtcNow.Date.AddDays(-90),DateTime.UtcNow.Date,ct) is BadRequestResult,
            "Unbounded sales report accepted.");

        // Explicit finance grant still must not elevate manager or staff to owner.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.Users(UserId,RoleId) SELECT 98,RoleId FROM dbo.[Role] WHERE RoleName='VendorManager';
            INSERT dbo.VendorUsers(VendorId,UserId) VALUES({b},98);
            INSERT dbo.RolePermission(RoleId,PageId,ListPage)
            SELECT r.RoleId,p.PageId,1 FROM dbo.[Role] r CROSS JOIN dbo.PageMaster p
            WHERE r.RoleName IN ('VendorManager','VendorStaff') AND p.PageCode='vendor.finance'
            AND NOT EXISTS(SELECT 1 FROM dbo.RolePermission rp WHERE rp.RoleId=r.RoleId AND rp.PageId=p.PageId);
            """);
        foreach(var user in new[]{2,98})
        {
            Check(!await access.CanAsync(b,user,VendorPermissionPages.Finance,ct) &&
                await Portal(user).Reports(b,null,null,ct) is NotFoundResult &&
                await Portal(user).FinanceReview(b,ct:ct) is NotFoundResult, "Non-owner finance exposure.");
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.VendorUsers SET IsActive=0 WHERE VendorId={b} AND UserId=1
            """);
        Check(await owner.Inventory(b,null,1,ct) is NotFoundResult &&
            await owner.Orders(b,ct:ct) is NotFoundResult, "Revoked vendor membership still accepted.");
        await tx.RollbackAsync();
        Console.WriteLine("Vendor SQL/EF controller integration: inventory, orders, returns, reports, profile and authorization passed.");
        if (Environment.GetEnvironmentVariable("MARKETPLACE_HTTP_TEST_PASSWORD") is { Length: > 0 } password &&
            Environment.GetEnvironmentVariable("MARKETPLACE_HTTP_TEST_IDS") is { Length: > 0 } idsPath)
            await PrepareHttpFixtureAsync(db,a,b,password,idsPath);
    }

    private static async Task PrepareHttpFixtureAsync(EcommerceDbContext db,int a,int b,string password,string idsPath)
    {
        // Complete only the legacy columns needed to start the real web application.
        // The caller already verified the disposable loopback database.
        await db.Database.ExecuteSqlRawAsync("""
            IF DB_NAME()<>N'MarketplaceMigrationTest' THROW 52900,'Disposable database required.',1;
            ALTER TABLE dbo.Users ADD FullName nvarchar(100) NULL,FullNameArb nvarchar(100) NULL,
                Email nvarchar(150) NULL,Phone nvarchar(12) NULL,PasswordHash nvarchar(max) NULL,
                CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),UpdatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                UpdatedBy int NULL,ProfileImage varbinary(max) NULL;
            ALTER TABLE dbo.[Role] ADD CreatedBy int NULL,UpdatedBy int NULL;
            CREATE TABLE dbo.CompanyDetails(CompanyId int PRIMARY KEY,CompanyName nvarchar(200),CompanyNameArabic nvarchar(200),Logo varbinary(max));
            INSERT dbo.CompanyDetails VALUES(1,N'CI Store',N'متجر الاختبار',NULL);
            CREATE TABLE dbo.colors(Id int PRIMARY KEY,Colorcode nvarchar(50));
            """);
        var hash=StaffPassword.Hash(password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.Users SET FullName=N'CI Vendor',FullNameArb=N'بائع الاختبار',
                Email=CASE WHEN UserId=1 THEN N'owner@example.test' ELSE N'staff@example.test' END,
                Phone=N'00000000',PasswordHash={hash} WHERE UserId IN (1,2);
            UPDATE dbo.VendorUsers SET IsActive=CASE WHEN VendorId={b} THEN 1 ELSE 0 END WHERE UserId IN (1,2);
            INSERT dbo.RolePermission(RoleId,PageId,ListPage)
            SELECT r.RoleId,p.PageId,1 FROM dbo.[Role] r CROSS JOIN dbo.PageMaster p
            WHERE r.RoleName='VendorStaff' AND p.PageCode IN ('vendor.dashboard','vendor.orders','vendor.finance')
                AND NOT EXISTS(SELECT 1 FROM dbo.RolePermission rp WHERE rp.RoleId=r.RoleId AND rp.PageId=p.PageId);
            """);
        await File.WriteAllTextAsync(idsPath,System.Text.Json.JsonSerializer.Serialize(new { a,b }));
        Console.WriteLine("Disposable HTTP vendor fixtures prepared.");
    }
}
