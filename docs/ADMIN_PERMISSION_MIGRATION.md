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
