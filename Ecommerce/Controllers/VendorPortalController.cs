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
        return View(store);
    }

    private IQueryable<VendorUser> StoreMembership(int id, int userId, bool write = false)
    {
        var query = access.Memberships(userId).Where(m => m.VendorId == id);
        if (write) query = query.Where(m => m.User.Role!.RoleName == VendorRoles.Owner ||
            m.User.Role.RoleName == VendorRoles.Manager);
        return query;
    }

    [HttpGet("store/{id:int}/listings")]
    public async Task<IActionResult> Listings(int id, string? search, int page = 1, CancellationToken ct = default)
    {
        if (!access.Enabled || !VendorPortalAuth.TryGetUserId(User, out var userId)) return NotFound();
        var store = await StoreMembership(id, userId).Select(m => new { m.Vendor.Name, m.User.Role!.RoleName })
            .SingleOrDefaultAsync(ct);
        if (store == null) return NotFound();
        return View(await VendorListingQueries.ReadPageAsync(db, id, store.Name, search, page,
            store.RoleName is VendorRoles.Owner or VendorRoles.Manager, false, ct));
    }

    [HttpGet("store/{id:int}/catalog")]
    public async Task<IActionResult> Catalog(int id, string? search, int page = 1, CancellationToken ct = default)
    {
        if (!access.Enabled || !VendorPortalAuth.TryGetUserId(User, out var userId) ||
            !await StoreMembership(id, userId, true).AnyAsync(ct)) return NotFound();
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
                ProductNameArb = p.NameArb }).ToListAsync(ct);
        return View(new VendorListingsPage { VendorId = id, Search = search, Page = page,
            Rows = rows.Take(size).ToList(), HasNext = rows.Count > size });
    }

    [HttpGet("store/{id:int}/listing")]
    public async Task<IActionResult> EditListing(int id, int listingId = 0, int productId = 0, CancellationToken ct = default)
    {
        if (!access.Enabled || !VendorPortalAuth.TryGetUserId(User, out var userId) ||
            !await StoreMembership(id, userId, true).AnyAsync(ct)) return NotFound();
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
        if (!await StoreMembership(id, userId, true).AnyAsync(ct)) return NotFound();
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
        var member = await StoreMembership(id, userId).Select(m => m.User.Role!.RoleName).SingleOrDefaultAsync(ct);
        if (member == null) return NotFound();
        var productName = await db.VendorListings.AsNoTracking().Where(l => l.VendorId == id &&
            l.VendorListingId == listingId).Select(l => l.Product.Name).SingleOrDefaultAsync(ct);
        if (productName == null) return NotFound();
        var balances = await db.Database.SqlQuery<ListingStockPage>($"""
            SELECT {id} AS VendorId, {listingId} AS ListingId, {productName} AS ProductName,
                   COALESCE(b.OnHandQuantity, CONVERT(bigint,0)) AS OnHand,
                   COALESCE(b.ReservedQuantity, CONVERT(bigint,0)) AS Reserved,
                   COALESCE(b.AvailableQuantity, CONVERT(bigint,0)) AS Available,
                   CONVERT(bit, CASE WHEN b.VendorListingId IS NULL THEN 0 ELSE 1 END) AS HasBalance,
                   CONVERT(bit,0) AS CanEdit, 1 AS Page, CONVERT(bit,0) AS HasNext
            FROM dbo.VendorListings l LEFT JOIN dbo.InventoryBalances b ON b.VendorListingId = l.VendorListingId
            WHERE l.VendorId = {id} AND l.VendorListingId = {listingId}
            """).SingleOrDefaultAsync(ct);
        if (balances == null) return NotFound();
        const int size = 25;
        page = Math.Clamp(page, 1, 100000);
        var rows = await db.Database.SqlQuery<StockMovementRow>($"""
            SELECT t.InventoryTransactionId, t.OnHandDelta, t.ReservedDelta, t.SourceType, t.Reason, t.CreatedAtUtc
            FROM dbo.InventoryTransactions t JOIN dbo.VendorListings l ON l.VendorListingId = t.VendorListingId
            WHERE l.VendorId = {id} AND l.VendorListingId = {listingId}
            """).OrderByDescending(t => t.InventoryTransactionId).Skip((page - 1) * size).Take(size + 1).ToListAsync(ct);
        balances.Page = page;
        balances.HasNext = rows.Count > size;
        balances.Movements = rows.Take(size).ToList();
        balances.CanEdit = member is VendorRoles.Owner or VendorRoles.Manager;
        balances.Adjustment = new StockAdjustmentForm { RequestId = Guid.NewGuid() };
        return View(balances);
    }

    [HttpPost("store/{id:int}/listing/{listingId:int}/stock")]
    public async Task<IActionResult> Stock(int id, int listingId, StockAdjustmentForm adjustment, CancellationToken ct)
    {
        if (!InventoryEnabled || !VendorPortalAuth.TryGetUserId(User, out var userId) ||
            !await StoreMembership(id, userId, true).AnyAsync(ct)) return NotFound();
        if (!ModelState.IsValid || adjustment.RequestId == Guid.Empty || adjustment.Delta == 0)
            return BadRequest("Invalid stock adjustment.");
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                EXEC dbo.PostInventoryAdjustment @VendorId={id}, @VendorListingId={listingId},
                    @UserId={userId}, @RequestId={adjustment.RequestId}, @Delta={adjustment.Delta}, @Reason={adjustment.Reason}
                """, ct);
        }
        catch (SqlException ex) when (ex.Number == 51312) { return NotFound(); }
        catch (SqlException ex) when (ex.Number is 51313 or 51314) { return Conflict("Stock or request conflict. Reload and review."); }
        catch (SqlException ex) when (ex.Number == 51310) { return BadRequest("Invalid stock adjustment."); }
        return RedirectToAction(nameof(Stock), new { id, listingId });
    }

    [HttpGet("store/{id:int}/orders")]
    public async Task<IActionResult> Orders(int id, int page = 1, CancellationToken ct = default)
    {
        if (!access.Enabled || !configuration.GetValue("Marketplace:VendorOrdersEnabled", false) ||
            !VendorPortalAuth.TryGetUserId(User, out var userId) ||
            !await StoreMembership(id, userId).AnyAsync(ct)) return NotFound();
        page = Math.Clamp(page, 1, 100000);
        var rows = await db.Database.SqlQuery<VendorOrderRow>($"""
            SELECT v.VendorOrderId,v.OrderId,v.MerchandiseSubtotal,o.CreatedAt,
                   (SELECT COUNT(*) FROM dbo.OrderItems i WHERE i.VendorOrderId=v.VendorOrderId) AS ItemCount
            FROM dbo.VendorOrders v JOIN dbo.Orders o ON o.OrderId=v.OrderId
            JOIN dbo.MarketplacePaymentAttempts p ON p.OrderId=o.OrderId
            WHERE v.VendorId={id} AND o.PaymentStatus=1 AND p.State='Paid'
            """).OrderByDescending(o => o.VendorOrderId).Skip((page-1)*25).Take(26).ToListAsync(ct);
        ViewBag.VendorId=id; ViewBag.Page=page; ViewBag.HasNext=rows.Count>25;
        return View(rows.Take(25).ToList());
    }

    [HttpGet("store/{id:int}/orders/{vendorOrderId:int}")]
    public async Task<IActionResult> OrderDetails(int id,int vendorOrderId,CancellationToken ct)
    {
        if (!access.Enabled || !configuration.GetValue("Marketplace:VendorOrdersEnabled", false) ||
            !VendorPortalAuth.TryGetUserId(User, out var userId) ||
            !await StoreMembership(id,userId).AnyAsync(ct)) return NotFound();
        var rows=await db.Database.SqlQuery<VendorOrderLine>($"""
            SELECT i.ProductNameSnapshot AS ProductName,i.ProductNameArbSnapshot AS ProductNameArb,
                   i.SellerSkuSnapshot AS SellerSku,i.Quantity,i.Price
            FROM dbo.OrderItems i JOIN dbo.VendorOrders v ON v.VendorOrderId=i.VendorOrderId AND v.OrderId=i.OrderId
            JOIN dbo.Orders o ON o.OrderId=v.OrderId
            JOIN dbo.MarketplacePaymentAttempts p ON p.OrderId=o.OrderId
            WHERE v.VendorId={id} AND v.VendorOrderId={vendorOrderId} AND o.PaymentStatus=1 AND p.State='Paid'
            """).ToListAsync(ct);
        if(rows.Count==0) return NotFound();
        ViewBag.VendorId=id; ViewBag.VendorOrderId=vendorOrderId;
        return View(rows);
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
