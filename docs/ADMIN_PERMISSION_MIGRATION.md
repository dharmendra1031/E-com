# Admin permission migration acceptance gate

The existing Admin login accepts only the Admin role. Do not enable AdminManager or AdminStaff logins until every reachable admin endpoint is classified, the legacy PageMaster mapping is reviewed, and restricted UI and direct HTTP tests pass. The existing SuperAdminOnly policy remains unchanged. No legacy permission is automatically promoted or reassigned.

Run the read-only scripts/security/02-admin-permission-preflight.sql on a restored deployment database. Review page IDs, names, existing grants, role usage, duplicate/orphan grants and scope conflicts. Never infer a page ID from a translated label or overwrite existing grants. Map existing pages to stable admin codes and add missing pages only after confirming the mapping.

## Proposed mapping (source navigation; database IDs not yet verified)

| Page code | Controller / landing action |
|---|---|
| admin.dashboard | AdminDashboard / Index |
| admin.orders | ManageOrders / PendingOrder |
| admin.customers | ManageCustomer / CustomerList |
| admin.stock | ProductStock / Index |
| admin.products | Products / ProductList |
| admin.categories | Category / Index |
| admin.subcategories | SubCategory / List |
| admin.topcategories | Category / TopCategory |
| admin.brands | Brand / Index |
| admin.deals | WekendDeals / WekendDealsList |
| admin.coupons | Coupon / Index |
| admin.banners | Banner / AddBanner |
| admin.notifications | SendNotification / Index |
| admin.blog | Blog / BlogList |
| admin.reports | Report / report actions |
| admin.company | Companydetails / Index |
| admin.areas | Areas / AreaList |
| admin.blocks | Blocks / BlockList |
| admin.contact | Contact / ContactList |
| admin.about | AboutUs / UpdateAbout |
| admin.theme | Changecolors / Index |

User, role, permission, vendor management, settlement/payment/refund review remain SuperAdminOnly unless explicitly approved. Public storefront and theme reads must remain public. Map all action methods, not just the navigation entries; GET form entry and POST mutations require matching operations. Report viewing and printing must remain distinct where supported. Never treat HTTP POST as automatically Update.

## Release blockers

- Verified legacy database mapping and explicit migration rehearsal.
- Deny-by-default authorization of restricted staff, including direct URLs and POST, with current role and grant validation.
- Permission-based sidebar, dashboard data, and action buttons; no access merely because a link is visible.
- Actual browser/HTTP regression with Admin, restricted roles, Customer, Vendor and anonymous sessions, including permission revocation, RTL and mobile.
- Separate legacy API security review: EcommerceApiController customer record ownership and admin notification endpoints, SmsController authorization, PaymentApiController and gateway evidence. Do not widen staff login while legacy endpoints remain unclassified.

The PortalAuthorizationChecks executable checks controller/action authorization metadata; it is not a substitute for real HTTP or browser testing. No production database was accessed by this change.

## Reviewed mapping migration and stale editor protection
After the vendor permission migration and preflight, create a temp table in one SQL session:
```sql
CREATE TABLE #AdminPageMapping(PageId int NOT NULL, ExpectedPageName nvarchar(100) NOT NULL, PageCode nvarchar(80) NOT NULL);
-- Insert actual reviewed PageId, exact current English name and approved stable code here.
-- Then execute scripts/marketplace/15-map-admin-pages.sql in the same session.
```
The script rejects empty/unknown mappings, duplicate IDs or codes, mismatched names/scopes, and conflicts with existing codes. It only assigns missing codes on reviewed existing Admin rows. It never creates grants or changes a legacy PageId, and is safe to repeat with the same mapping. The disposable SQL fixture maps one test page and verifies that its existing grant survives. The existing Role Permission editor now checks a deterministic revision of the role, page set and grants within the serializable save transaction; stale screens cannot overwrite a changed matrix. Permission List pagination is performed in SQL. No restricted Admin login has been enabled and no deployed database mapping has been guessed. Actual endpoint action classification, enforcement, restricted navigation and acceptance remain required.

## First permission-controlled module: Brand (development pilot only)
Authentication__RestrictedAdminPilotEnabled defaults false and is honored only in Development. The existing Admin role remains unchanged. Explicit active AdminManager/AdminStaff roles can sign in only if a reviewed admin.brands mapping exists and ListPage is granted. No role or right is auto-created. Their cookie contains the actual role, role ID and credential version; deactivation, role reassignment or credential change invalidates it. The global MVC authorization filter denies all unclassified actions to these two roles, including legacy anonymous APIs; only Brand actions, logout, access denied and language switch are admitted. Brand list and autocomplete require ListPage; GET add/edit forms and POST saves distinguish CreatePage and UpdatePage by record ID; Delete requires DeletePage; writes require view. Sidebar and Brand buttons use the same scoped permission lookup. Other Admin routes and sensitive SuperAdmin functions are not opened. Brand audit IDs now use the signed-in user. Existing Admin retains full Brand access. No actual deployed mapping, role creation, browser tests, concurrent revocation during a running mutation or production activation performed. Expand action classification incrementally before enabling production; reports, dashboard, orders and other modules remain unavailable to restricted roles.

## Catalog permission expansion (development pilot)
Brand, Category, Subcategory and Product now share one request-scoped lookup of mapped page grants, grouped by exact stable code; duplicate grants fail closed. Restricted login redirects to the first accessible implemented page. All four list pages and their add/edit/delete controls follow their corresponding permissions, with the same checks in controller actions and the default-deny MVC filter. Product image removal is an edit operation; product editor dropdown data is available to product creators/editors, not permission to modify category masters. Product create/edit/delete POST actions require antiforgery. Restricted product creation requires zero opening stock (server enforced); the existing Admin retains the previous opening stock workflow. The existing Top Category combined editor remains Admin-only until its own UI and permissions are classified. Category/subcategory/product audit fields use the authenticated user. The pilot remains off by default and Development only. No live legacy database mapping or role assignment has been made. Browser/HTTP denial tests, destructive product/category deletion integrity with real marketplace references, and inventory write permission review remain release gates. No new tables or packages.

## Top Category and read-only operations pilot
Corrects the misplaced product audit assignment that broke the previous build. Adds admin.topcategories as a distinct page: list, create, update and delete are checked independently; writes require antiforgery and actual actor ID. Adds admin.stock and admin.orders read-only restricted access using existing pages; their grant write flags are intentionally suppressed until the marketplace stock and fulfillment procedures are integrated. Direct legacy write/status/print endpoints retain Admin-only authorization. Restricted staff see only applicable navigation; stock editor, order status and invoice print controls are omitted and the existing JS handles their absence. These screens still read legacy stock/order projections, not a complete vendor inventory or fulfillment workspace. The existing Admin manual payment/status paths require separate integrity audit before production. No live mapping, deployment or browser verification performed. Guest checkout remains deferred.

## Marketplace inventory and legacy order integrity
Admin inventory has a development-only vendor listing register and movement history when VendorMasterEnabled and VendorInventoryEnabled are true. Restricted staff require admin.stock ListPage to view and UpdatePage to adjust; legacy AddStock stays Admin-only and rejects listed products whenever the marketplace listing table exists, even if the feature is disabled. Both portals share ListingStockQueries and the stock detail view, and post via the existing PostInventoryAdjustment procedure. Reapply inventory/04-post-adjustment.sql before using the new page. The optional AdminPortal argument defaults to vendor behavior; SQL independently checks active Admin or restricted Admin role plus mapped view/update permission under transaction locks. Existing listing ownership, request idempotency, available stock and audit checks are unchanged. The SQL fixture exercises missing and revoked admin grants and duplicate posting. Legacy order status mutations and password verification reject marketplace orders; manual payment mutation is limited to legacy COD. Online payments must be confirmed through a verified gateway flow. The existing legacy EF query filter is retained; explicit mutation guards also reject marketplace orders. The online payment change button is hidden on legacy order detail. Legacy stock and marketplace balances are separate and no production stock migration is performed. Browser testing, legacy stock writers outside this controller, marketplace fulfillment delivery and deployed page mapping remain pending.

## Customer and location master pilot
Restricted customer grants are view-only, including profile and saved addresses; legacy order details additionally require admin.orders view. Password reset and block/unblock remain Admin-only. Customer web cookies now include a credential version and are rejected on password change, inactive/deleted customer or invalid role; existing pre-upgrade cookies require sign-in. Legacy session-based endpoints still require independent audit. Areas and blocks support mapped view/create/edit; restricted deletion and moving existing blocks between areas are denied until address integrity is audited. Admin-only deletion now has explicit CSRF checks. Location list pagination stays in SQL. No mapping rows or grants are auto-created; pilot remains disabled by default. Browser acceptance remains pending.

## Marketing permission pilot
Mapped banners support view/create/edit (no delete action), blogs support view/create/edit/delete. Coupon and weekend deals are read-only for restricted staff pending marketplace discount policy and checkout integrity audit; pricing writes and edit forms remain Admin-only. All marketing POST routes explicitly validate antiforgery. Banner posting shares one save path and enforces the existing three-banner limit in a serializable transaction. Blog and banner uploads have request and decoded dimension limits, use the existing image compression path; further image decoder and browser verification remain required. Blog and deal audit fields use the logged-in ID; coupon/deal/blog lists paginate in SQL. No grants are automatically assigned. Pilot remains development-only, disabled by default.
