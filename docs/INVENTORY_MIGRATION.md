# Inventory engine migration — stage 1

Scope: SQL Server foundation only. The running application continues using legacy stock. Do not deploy the new balance as authoritative until the cutover gates pass.

## Source audit

| Path | Existing behavior |
|---|---|
| WebCheckoutController.PlaceOrder | ProductStock sum minus all historical OrderItems; no receipt-row decrement |
| EcommerceApiController.PlaceOrder | ProductStock sum minus OrderItems, then also decrements receipt rows |
| PaymentApiController.SendPayment / SaveOrderAsync | Checks Products.Stock, checks historical aggregates, then decrements Products.Stock |
| ProductsController.Create | Seeds Products.Stock and ProductStock |
| ProductStockController.AddStock | Adds a ProductStock row |
| EcommerceApiController.CancelOrder | Adds ProductStock rows for order items, updates order status |
| ManageOrdersController.UpdateOrderStatus | Changes order status and history without stock release |
| ReportController.StockReportPrint | Calls dbo.sp_StockReport; definition not in repository |

Stock receipts are not a trustworthy immutable ledger because mobile orders modify old rows. Reconstructing physical quantity from current tables alone is not safe. Payment type, order status and legacy stock differences require reconciliation; do not silently clamp negative stock or use Products.Stock as truth.

## SQL foundation

- InventoryBalances: one row per current product, on-hand and reserved quantities, computed available quantity, rowversion and nonnegative constraints.
- InventoryReservations: one reservation per order/product with expiry and lifecycle status.
- InventoryTransactions: immutable-by-application movement history; unique source event to prevent repeated stock posting. DB permissions must restrict direct updates/deletes before launch.
- Release is a reservation status change and movement, not an extra table.

No EF migration history or existing database scripts were found in the inspected source. Use explicitly run, reviewed SQL scripts rather than auto-creating the production schema at startup.

## Safe rollout

1. Restore a current database backup in a separate SQL Server instance. Stop writes or take a consistent snapshot for diagnostics. Run scripts/inventory/01-reconcile.sql. Review negative values, orphan rows, historical discrepancies, all order/payment statuses and actual database modules/triggers. Export full discrepancies securely if required; diagnostic sample is limited to 200 rows.
2. Run 02-create-schema.sql on the restored database. It is additive and leaves the three new tables empty. No opening balances are inferred. Verify the installed definitions against the script (a repeat run only checks table existence). 
3. Obtain approved physical balances and open unpaid orders per product. Establish opening on-hand/reserved amounts with auditable sources. Reconcile product IDs, pending orders, payments, cancelled orders and returned quantities; keep an exception register. A stocktake or reliable external ledger may be necessary. No production backfill until these decisions are signed off.
4. Implement a single inventory service used by product creation, stock receipt, website checkout, mobile checkout, gateway order, cancellation, return and reservation expiry. Use a single database transaction for order, inventory and movement rows. Reserve via conditional UPDATE with AvailableQuantity >= requested quantity; check affected row count. Lock products in consistent order, use a unique source key and compare existing request payload before accepting a retry. Consume/release by a guarded reservation-state transition with movement in same transaction. Never hold a SQL transaction open during gateway calls. Reconcile the gateway before releasing potentially paid orders.
5. Switch reads (catalog, cart, product detail, admin stock, dashboard, mobile APIs, RDLC stored procedures) together after shadow comparison. Rehearse maintenance-window cutover on restored production data. Freeze legacy writes before final reconciliation; keep old tables and IDs. Do not deploy a mixed-writer system. Rollback before new-engine writes is straightforward; after writes, reconcile and replay approved movements before reverting.
6. After single-vendor verification, migrate the inventory key to seller listing/location scope for marketplace support. Do not duplicate stock per product or invent vendor balances.

## Acceptance gates (not passed by build alone)

- SQL schema test, then actual restored database schema compatibility.
- Two independent concurrent checkouts for last available item: only one succeeds.
- Multi-item reservation rolls back on any unavailable item.
- Repeated same request does not double-reserve; changed payload with same source is rejected.
- Paid confirmation, repeated callback, expiration, cancellation and return each apply correct movement once.
- Old order and report totals preserved; no unaccounted stock differences.
- Actual SQL execution plans and load tests for 1,000,000 products plus realistic order history; record p95 latency, locks, deadlocks and throughput.
- English/Arabic browser and mobile API acceptance.

The CI fixture checks schema and basic constraints only. It is not a test of the running inventory service, actual concurrency, payment gateway or production data. Do not claim zero overselling yet.
