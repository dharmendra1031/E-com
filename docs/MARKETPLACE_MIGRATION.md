# Marketplace conversion — stage 1
Baseline: development commit 7dd5e498ec9f1cf17c87fbc4fb3f6b1d0f9a1a2b.

## Architecture
Reuse existing ASP.NET Core MVC, mobile APIs, EF Core, SQL Server and RDLC. One storefront and one database. Products remain the shared catalog; VendorListings hold seller-specific SKU, price and approval. One offer per seller/catalog product for the initial release; distinct variants require distinct catalog products. Vendors may share a catalog product, not a stock balance. Existing Role/RolePermission remains the permission system; VendorUsers adds ownership scope. Membership alone never grants access. Pending and suspended vendors and unapproved listings are not purchasable after integration. Stage 1 deliberately does not expose new endpoints or EF entities.

## Current code dependencies
- Products.cs currently contains Price and Stock, ProductStock.cs is keyed by ProductId.
- CartItem.cs and OrderItemmodel.cs reference ProductId only.
- WebCheckoutController.PlaceOrder, EcommerceApiController and PaymentApiController contain separate checkout/inventory paths. PaymentApiController still changes Products.Stock.
- OrderModel has one order status; ReturnRequest refers to a whole order; Payments refers to OrderId.
- ExecutePaymentRequest already exposes Suppliers; gateway account eligibility and refund/settlement behavior are unverified.
- EcommerceDbContext has no marketplace sets. Existing inventory migration is staged, not evidence of live cutover. See INVENTORY_MIGRATION.md.

## Migration stages
1. Add Vendors, VendorUsers, VendorListings via scripts/marketplace/01-create-schema.sql. Run against a restored backup; no seed or live change. Disposable fixture test-schema.sql uses sqlcmd -b, working directory scripts/marketplace. It covers empty migration, repeat run, two sellers for one product, duplicate seller SKU/product, negative price and invalid references. Do not use fixture on production.
2. Vendor master and approval, existing login/role integration, server-derived membership scope. Cross-vendor read/write and file/report access tests. Vendor registration policy and KYC to be confirmed.
3. Catalog listing create/edit/approval; explicitly map existing store to a verified first-party vendor and existing products to listings after checking SKU collisions. Preserve IDs, prices and histories; no fabricated vendors or balances.
4. Complete existing inventory reconciliation; adapt the staged balance, reservation and movement schema to listing scope before multi-vendor activation. No second authoritative stock engine. Shared transactional reserve/consume/release across web and mobile, cancellation, returns, expiry. No production cutover before approved opening stock.
5. Cart references VendorListingId; resolve price, status, stock and ownership on server. Use one checkout service, parent order and vendor sub-orders, immutable item/price/commission snapshots, deterministic discount/charge allocations, idempotency, atomic reservations. Old clients must have explicit compatible version/offer handling; never silently select a seller.
6. MyFatoorah verified payment and callback, exact amount/currency/order binding and retry-safe posting. Gateway settlement/split availability depends on merchant approval. Design auditable vendor payable and settlement ledger, refunds and reversals, COD reconciliation. No automatic payout until provider capability and business rules approved.
7. Existing customer and admin views plus vendor-scoped portal; independent sub-order fulfillment, line-level return and refund, seller invoices and reporting. English/Arabic RTL and mobile responsive. Extend existing RDLC rather than duplicate report engine.
8. Validate SQL plans and realistic one-million-product/offer workload; server paging, indexed queries, no per-row stock queries or base64 image payloads in listing grids. Measure p95 latency and concurrency instead of claiming speed from schema.
9. Rehearse production upgrade on restored backup, verify stored procedures, reconcile totals and inventory, switch writers together, feature-gate new sales until all tests pass. Preserve historical order and report totals, do not change main.

## Decisions requiring business confirmation
Commission rate and basis, shipping responsibility and allocation, seller KYC, who collects customer money, payout timing, tax/invoice responsibility, cancellation/return policy and whether buyers select offers or a default offer. No hardcoded commission, live gateway assumptions, or production changes.

## Stage 1 status
SQL and fixture authored only; execution against SQL Server and restored data still required. No storefront changes, vendor login, stock cutover or multi-vendor orders yet.
