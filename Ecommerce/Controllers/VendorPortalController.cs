using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Data;
using Microsoft.Data.SqlClient;

namespace EcommerceWebApplication.Controllers;

[Route("vendor-portal")]
[Authorize(Policy = VendorPortalAuth.Policy)]
[AutoValidateAntiforgeryToken]
public class VendorPortalController(EcommerceDbContext db, VendorPortalAccess access, IConfiguration configuration) : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login() => access.Enabled ? View(new VendorLoginForm()) : NotFound();

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(VendorPortalAuth.LoginRatePolicy)]
    public async Task<IActionResult> Login(VendorLoginForm form, CancellationToken ct)
    {
        if (!access.Enabled) return NotFound();
        if (!ModelState.IsValid) return View(form);
        var candidates = await db.Users.AsNoTracking().Include(u => u.Role)
            .Where(u => u.IsActive && u.Email == form.Email).Take(2).ToListAsync(ct);
        var user = candidates.Count == 1 ? candidates[0] : null;
        if (user?.Role is not { IsActive: true } || !VendorRoles.IsVendorRole(user.Role.RoleName) ||
            !StaffPassword.Verify(user.PasswordHash, form.Password, false, out _) ||
            !await access.Memberships(user.UserId).AnyAsync(ct))
        {
            ModelState.AddModelError("", System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
                ? "بيانات الدخول غير صحيحة أو الحساب غير متاح." : "Invalid credentials or account unavailable.");
            form.Password = "";
            return View(form);
        }
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("UserId", user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.FullName ?? ""),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Role, user.Role.RoleName!),
            new Claim(VendorPortalAuth.CredentialClaim, VendorPortalAuth.CredentialVersion(user.PasswordHash!))
        }, VendorPortalAuth.Scheme);
        await HttpContext.SignInAsync(VendorPortalAuth.Scheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(3) });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!access.Enabled) return NotFound();
        if (!VendorPortalAuth.TryGetUserId(User, out var userId)) return Forbid(VendorPortalAuth.Scheme);
        var vendors = await access.Memberships(userId).OrderBy(m => m.Vendor.Name).ThenBy(m => m.VendorId)
            .Select(m => new VendorPortalStore { VendorId = m.VendorId, Code = m.Vendor.VendorCode,
                Name = m.Vendor.Name, NameArb = m.Vendor.NameArb }).ToListAsync(ct);
        if (vendors.Count == 1) return RedirectToAction(nameof(Dashboard), new { id = vendors[0].VendorId });
        return View(vendors);
    }

    [HttpGet("store/{id:int}")]
    public async Task<IActionResult> Dashboard(int id, CancellationToken ct)
    {
        if (!access.Enabled) return NotFound();
        if (!VendorPortalAuth.TryGetUserId(User, out var userId)) return Forbid(VendorPortalAuth.Scheme);
        var store = await access.Memberships(userId).Where(m => m.VendorId == id)
            .Select(m => new VendorPortalStore { VendorId = m.VendorId, Code = m.Vendor.VendorCode,
                Name = m.Vendor.Name, NameArb = m.Vendor.NameArb }).SingleOrDefaultAsync(ct);
        if (store == null) return NotFound();
        if (!await access.CanAsync(id, userId, VendorPermissionPages.Dashboard, ct))
        {
            if (await access.CanAsync(id, userId, VendorPermissionPages.Listings, ct))
                return RedirectToAction(nameof(Listings), new { id });
            if (configuration.GetValue("Marketplace:VendorOrdersEnabled", false) &&
                await access.CanAsync(id, userId, VendorPermissionPages.Orders, ct))
                return RedirectToAction(nameof(Orders), new { id });
            if (configuration.GetValue("Marketplace:FinancePreviewEnabled", false) &&
                await access.CanAsync(id, userId, VendorPermissionPages.Finance, ct))
                return RedirectToAction(nameof(FinanceReview), new { id });
            return Forbid(VendorPortalAuth.Scheme);
        }
        ViewBag.CanListProducts = await access.CanAsync(id, userId, VendorPermissionPages.Listings, ct);
        ViewBag.CanCreateListing = await access.CanAsync(id, userId, VendorPermissionPages.Listings, ct, create: true);
        if (ViewBag.CanListProducts == true)
        {
        var products = await db.VendorListings.AsNoTracking().Where(l => l.VendorId == id)
            .GroupBy(l => l.VendorId)
            .Select(g => new { Total = g.Count(), Approved = g.Count(l => l.Status == "Approved" && l.Product.IsActive),
                Pending = g.Count(l => l.Status == "Pending") }).SingleOrDefaultAsync(ct);
        ViewBag.ProductCount = products?.Total ?? 0;
        ViewBag.ApprovedCount = products?.Approved ?? 0;
        ViewBag.PendingCount = products?.Pending ?? 0;
        if (InventoryEnabled && await access.CanAsync(id, userId, VendorPermissionPages.Stock, ct))
            ViewBag.OutOfStock = await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.VendorListings l
                JOIN dbo.Products p ON p.ProductId=l.ProductId
                LEFT JOIN dbo.InventoryBalances b ON b.VendorListingId=l.VendorListingId
                WHERE l.VendorId={id} AND l.Status='Approved' AND p.IsActive=1 AND COALESCE(b.AvailableQuantity,0)=0
                """).SingleAsync(ct);
        }
        if (configuration.GetValue("Marketplace:VendorOrdersEnabled",false) &&
            await access.CanAsync(id, userId, VendorPermissionPages.Orders, ct))
            ViewBag.ToPack = await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.VendorOrders v JOIN dbo.Orders o ON o.OrderId=v.OrderId
                WHERE v.VendorId={id} AND o.PaymentStatus=1 AND v.FulfillmentStatus='AwaitingPacking'
                    AND EXISTS (SELECT 1 FROM dbo.MarketplacePaymentAttempts p WHERE p.OrderId=o.OrderId AND p.State='Paid')
                """).SingleAsync(ct);
        return View(store);
    }

    private IQueryable<VendorUser> StoreMembership(int id, int userId) =>
        access.Memberships(userId).Where(m => m.VendorId == id);

    [HttpGet("store/{id:int}/listings")]
    public async Task<IActionResult> Listings(int id, string? search, int page = 1, CancellationToken ct = default, string? filter = null, bool partial = false)
    {
        if (!access.Enabled || !VendorPortalAuth.TryGetUserId(User, out var userId)) return NotFound();
        if (!await access.CanAsync(id, userId, VendorPermissionPages.Listings, ct)) return NotFound();
        var canStock = InventoryEnabled && await access.CanAsync(id, userId, VendorPermissionPages.Stock, ct);
        var store = await StoreMembership(id, userId).Select(m => new { m.Vendor.Name })
            .SingleOrDefaultAsync(ct);
        if (store == null) return NotFound();
        if (filter is not ("Approved" or "Pending" or "Inactive" or "OutOfStock") ||
            (filter == "OutOfStock" && !canStock)) filter = null;
        var model = await VendorListingQueries.ReadPageAsync(db, id, store.Name, search, page,
            await access.CanAsync(id, userId, VendorPermissionPages.Listings, ct, update: true), false, ct, filter);
        model.CanCreate = await access.CanAsync(id, userId, VendorPermissionPages.Listings, ct, create: true);
        model.CanViewStock = canStock;
        return partial ? PartialView("_VendorListings", model) : View(model);
    }

    [HttpGet("store/{id:int}/catalog")]
    public async Task<IActionResult> Catalog(int id, string? search, int page = 1, CancellationToken ct = default, bool partial = false)
    {
        if (!access.Enabled || !VendorPortalAuth.TryGetUserId(User, out var userId) ||
            !await access.CanAsync(id, userId, VendorPermissionPages.Listings, ct, create: true)) return NotFound();
        const int size = 25;
        page = Math.Clamp(page, 1, 100000);
        search = search?.Trim();
        var query = db.Products.AsNoTracking().Where(p => p.IsActive &&
            !db.VendorListings.Any(l => l.VendorId == id && l.ProductId == p.ProductId));
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.Contains(search) || p.NameArb.Contains(search) ||
                (p.SKUId != null && p.SKUId.Contains(search)) ||
                (p.Ds_Barcode != null && p.Ds_Barcode.Contains(search)));
        var rows = await query.OrderBy(p => p.ProductId).Skip((page - 1) * size).Take(size + 1)
            .Select(p => new VendorListingsPage.Row { ProductId = p.ProductId, ProductName = p.Name,
                ProductNameArb = p.NameArb, CatalogSku = p.SKUId, Barcode = p.Ds_Barcode }).ToListAsync(ct);
        var model = new VendorListingsPage { VendorId = id, Search = search, Page = page,
            Rows = rows.Take(size).ToList(), HasNext = rows.Count > size };
        return partial ? PartialView("_CatalogRegister", model) : View(model);
    }

    [HttpGet("store/{id:int}/listing")]
    public async Task<IActionResult> EditListing(int id, int listingId = 0, int productId = 0, CancellationToken ct = default)
    {
        if (!access.Enabled || !VendorPortalAuth.TryGetUserId(User, out var userId) ||
            !await access.CanAsync(id, userId, VendorPermissionPages.Listings, ct,
                create: listingId == 0, update: listingId != 0)) return NotFound();
        if (listingId != 0)
        {
            var row = await db.VendorListings.AsNoTracking().Where(l => l.VendorId == id && l.VendorListingId == listingId)
                .Select(l => new { l.ProductId, l.SellerSku, l.Price, l.RowVersion, l.Product.Name })
                .SingleOrDefaultAsync(ct);
            if (row == null) return NotFound();
            return View(new VendorListingForm { VendorId = id, ListingId = listingId, ProductId = row.ProductId,
                SellerSku = row.SellerSku, Price = row.Price, Version = Convert.ToBase64String(row.RowVersion),
                ProductName = row.Name });
        }
        var product = await db.Products.AsNoTracking().Where(p => p.ProductId == productId && p.IsActive)
            .Select(p => new { p.Name }).SingleOrDefaultAsync(ct);
        if (product == null) return NotFound();
        return View(new VendorListingForm { VendorId = id, ProductId = productId, ProductName = product.Name });
    }

    [HttpPost("store/{id:int}/listing")]
    public async Task<IActionResult> EditListing(int id, VendorListingForm form, CancellationToken ct)
    {
        if (!access.Enabled || !VendorPortalAuth.TryGetUserId(User, out var userId) || form.VendorId != id)
            return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await access.CanAsync(id, userId, VendorPermissionPages.Listings, ct,
            create: form.ListingId == 0, update: form.ListingId != 0)) return NotFound();
        var product = await db.Products.AsNoTracking().Where(p => p.ProductId == form.ProductId && p.IsActive)
            .Select(p => new { p.Name }).SingleOrDefaultAsync(ct);
        if (product == null) return NotFound();
        form.ProductName = product.Name;
        if (!ModelState.IsValid) return View(form);
        var sku = form.SellerSku.Trim();
        if (sku.Length == 0 || decimal.Round(form.Price, 3) != form.Price)
        {
            ModelState.AddModelError("", "SKU is required; price may have at most three decimal places.");
            return View(form);
        }

        VendorListing listing;
        if (form.ListingId == 0)
        {
            listing = new VendorListing { VendorId = id, ProductId = form.ProductId,
                CreatedAtUtc = DateTime.UtcNow, SellerSku = sku, Price = form.Price };
            db.VendorListings.Add(listing);
        }
        else
        {
            var existing = await db.VendorListings.SingleOrDefaultAsync(l =>
                l.VendorId == id && l.VendorListingId == form.ListingId && l.ProductId == form.ProductId, ct);
            if (existing == null) return NotFound();
            if (!VendorListingQueries.TryVersion(form.Version, out var version) ||
                !existing.RowVersion.SequenceEqual(version)) return Conflict("Listing changed; reload and try again.");
            if (existing.SellerSku == sku && existing.Price == form.Price)
                return RedirectToAction(nameof(Listings), new { id });
            listing = existing;
            listing.SellerSku = sku;
            listing.Price = form.Price;
            listing.Status = "Pending";
        }
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return Conflict("Listing changed; reload and try again."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError("", "This product or SKU is already listed for this vendor.");
            return View(form);
        }
        return RedirectToAction(nameof(Listings), new { id });
    }

    private bool InventoryEnabled => access.Enabled && configuration.GetValue("Marketplace:VendorInventoryEnabled", false);

    [HttpGet("store/{id:int}/listing/{listingId:int}/stock")]
    public async Task<IActionResult> Stock(int id, int listingId, int page = 1, CancellationToken ct = default)
    {
        if (!InventoryEnabled || !VendorPortalAuth.TryGetUserId(User, out var userId)) return NotFound();
        if (!await access.CanAsync(id, userId, VendorPermissionPages.Listings, ct) ||
            !await access.CanAsync(id, userId, VendorPermissionPages.Stock, ct)) return NotFound();
        var canEdit = await access.CanAsync(id,userId,VendorPermissionPages.Stock,ct,update:true);
        var balances = await ListingStockQueries.ReadAsync(db,id,listingId,page,canEdit,ct);
        if (balances == null) return NotFound();
        return View(balances);
    }

    [HttpPost("store/{id:int}/listing/{listingId:int}/stock")]
    public async Task<IActionResult> Stock(int id, int listingId, StockAdjustmentForm adjustment, CancellationToken ct)
    {
        if (!InventoryEnabled || !VendorPortalAuth.TryGetUserId(User, out var userId) ||
            !await access.CanAsync(id, userId, VendorPermissionPages.Listings, ct) ||
            !await access.CanAsync(id, userId, VendorPermissionPages.Stock, ct, update: true)) return NotFound();
        if (!ModelState.IsValid || adjustment.RequestId == Guid.Empty ||
            adjustment.Quantity is < 1 or > 1000000000 || adjustment.Direction is not ("Add" or "Reduce") ||
            string.IsNullOrWhiteSpace(adjustment.Reason))
        {
            TempData["StockError"] = "Enter a valid quantity and reason. / أدخل كمية وسبباً صحيحين.";
            return RedirectToAction(nameof(Stock), new { id, listingId });
        }
        var delta = adjustment.Direction == "Add" ? adjustment.Quantity : -adjustment.Quantity;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                EXEC dbo.PostInventoryAdjustment @VendorId={id}, @VendorListingId={listingId},
                    @UserId={userId}, @RequestId={adjustment.RequestId}, @Delta={delta}, @Reason={adjustment.Reason.Trim()}
                """, ct);
        }
        catch (SqlException ex) when (ex.Number == 51312) { return NotFound(); }
        catch (SqlException ex) when (ex.Number is 51313 or 51314 or 51310)
        {
            TempData["StockError"] = "Stock not changed. Review available quantity and retry. / لم يتغير المخزون. راجع الكمية المتاحة.";
            return RedirectToAction(nameof(Stock), new { id, listingId });
        }
        TempData["StockSuccess"] = "Stock updated. / تم تحديث المخزون.";
        return RedirectToAction(nameof(Stock), new { id, listingId });
    }

    [HttpGet("store/{id:int}/finance")]
    [ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
    public async Task<IActionResult> FinanceReview(int id,int page=1,CancellationToken ct=default,bool partial=false)
    {
        if (!access.Enabled || !configuration.GetValue("Marketplace:FinancePreviewEnabled",false) ||
            !VendorPortalAuth.TryGetUserId(User,out var actor) ||
            !await access.CanAsync(id, actor, VendorPermissionPages.Finance, ct)) return NotFound();
        page=Math.Clamp(page,1,100000);
        var rows=await db.Database.SqlQuery<MarketplaceFinanceRow>($"""
            SELECT f.VendorOrderId,f.OrderId,f.VendorId,f.VendorName,f.MerchandiseSubtotal,f.CommissionRatePercent,
                f.CommissionAmount,f.MerchandiseAfterCommission,f.FulfillmentStatus,f.ReviewState,
                f.ReceivedReturnQuantity,f.ReturnedMerchandiseValue,f.RemainingMerchandiseValue,f.OpenReturnQuantity,
                f.VerifiedRefundValue,f.LedgerNet,f.LedgerCommissionReversal,f.SettlementEligible,f.LedgerCurrent,
                f.LedgerVersion,f.TestPaidAmount,f.TestOutstanding,f.ApprovalId,f.ApprovedAtUtc,f.TestPayoutReference,f.CanRecordTestPayout,
                f.LastTestPayoutReference,f.LastTestPayoutAtUtc
            FROM dbo.MarketplaceSettlementApprovalReview f WHERE f.VendorId={id}
                AND EXISTS(SELECT 1 FROM dbo.VendorUsers m JOIN dbo.Users u ON u.UserId=m.UserId
                    JOIN dbo.[Role] r ON r.RoleId=u.RoleId JOIN dbo.Vendors v ON v.VendorId=m.VendorId
                    WHERE m.VendorId=f.VendorId AND m.UserId={actor} AND m.IsActive=1 AND u.IsActive=1
                    AND r.IsActive=1 AND r.RoleName='VendorOwner' AND v.Status='Approved'
                    AND EXISTS(SELECT 1 FROM dbo.RolePermission rp JOIN dbo.PageMaster pm ON pm.PageId=rp.PageId
                        WHERE rp.RoleId=u.RoleId AND pm.PortalScope='Vendor' AND pm.PageCode='vendor.finance' AND rp.ListPage=1))
            """).OrderByDescending(r=>r.VendorOrderId).Skip((page-1)*25).Take(26).ToListAsync(ct);
        ViewBag.Page=page;ViewBag.HasNext=rows.Count>25;ViewBag.VendorId=id;
        return partial ? PartialView("_FinanceRegister", rows.Take(25).ToList()) : View(rows.Take(25).ToList());
    }

    [HttpGet("store/{id:int}/orders")]
    public async Task<IActionResult> Orders(int id, int page = 1, CancellationToken ct = default, string? status = null, bool partial = false)
    {
        if (!access.Enabled || !configuration.GetValue("Marketplace:VendorOrdersEnabled", false) ||
            !VendorPortalAuth.TryGetUserId(User, out var userId) ||
            !await access.CanAsync(id, userId, VendorPermissionPages.Orders, ct)) return NotFound();
        page = Math.Clamp(page, 1, 100000);
        if (!VendorOrderQueries.ValidStatus(status)) status=null;
        var rows=await VendorOrderQueries.ReadPageAsync(db,id,status,page,ct);
        ViewBag.VendorId=id; ViewBag.Page=page; ViewBag.HasNext=rows.Count>25; ViewBag.Status=status;
        return partial ? PartialView("_OrdersRegister", rows.Take(25).ToList()) : View(rows.Take(25).ToList());
    }

    [HttpGet("store/{id:int}/orders/{vendorOrderId:int}")]
    public async Task<IActionResult> OrderDetails(int id,int vendorOrderId,CancellationToken ct)
    {
        if (!access.Enabled || !configuration.GetValue("Marketplace:VendorOrdersEnabled", false) ||
            !VendorPortalAuth.TryGetUserId(User, out var userId) ||
            !await access.CanAsync(id, userId, VendorPermissionPages.Orders, ct)) return NotFound();
        var rows=await VendorOrderQueries.ReadLinesAsync(db,id,vendorOrderId,ct);
        if(rows.Count==0) return NotFound();
        ViewBag.VendorId=id; ViewBag.VendorOrderId=vendorOrderId;
        ViewBag.FulfillmentHistory=await VendorOrderQueries.ReadHistoryAsync(db,id,vendorOrderId,ct);
        ViewBag.CanAdvance = configuration.GetValue("Marketplace:VendorFulfillmentEnabled", false) &&
            await access.CanAsync(id, userId, VendorPermissionPages.Orders, ct, update: true);
        return View(rows);
    }

    [HttpPost("store/{id:int}/orders/{vendorOrderId:int}/fulfillment")]
    public async Task<IActionResult> AdvanceFulfillment(int id,int vendorOrderId,string expectedStatus,string newStatus,CancellationToken ct)
    {
        if (!access.Enabled || !configuration.GetValue("Marketplace:VendorOrdersEnabled", false) ||
            !configuration.GetValue("Marketplace:VendorFulfillmentEnabled", false) ||
            !VendorPortalAuth.TryGetUserId(User,out var actor) ||
            !await access.CanAsync(id, actor, VendorPermissionPages.Orders, ct, update: true)) return NotFound();
        if (!ModelState.IsValid || !VendorOrderQueries.CanAdvance(expectedStatus,newStatus)) return BadRequest();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                EXEC dbo.AdvanceVendorFulfillment @VendorId={id},@VendorOrderId={vendorOrderId},
                    @ActorId={actor},@ExpectedStatus={expectedStatus},@NewStatus={newStatus}
                """,ct);
            TempData["FulfillmentMessage"]="Order status saved. / تم حفظ حالة الطلب.";
        }
        catch (SqlException ex) when (ex.Number is >=52000 and <=52003)
        {
            TempData["FulfillmentMessage"]="Order status not changed. Refresh and review. / لم تتغير حالة الطلب.";
        }
        return RedirectToAction(nameof(OrderDetails),new { id,vendorOrderId });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(VendorPortalAuth.Scheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("denied")]
    public IActionResult Denied() => StatusCode(StatusCodes.Status403Forbidden);
}
