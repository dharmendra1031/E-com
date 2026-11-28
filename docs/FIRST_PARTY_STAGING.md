# First-party catalog staging (pre-cutover)

This is a restored-database rehearsal, not a live conversion. Create and verify the actual first-party vendor through existing Vendor Management. Supply its explicit approved VendorId. Review each legacy ProductId, seller SKU, and price; missing SKU, price discrepancies, and collisions must be resolved by the operator, not auto-generated. Never assign other sellers' stock to the first party.

In a single sqlcmd session, after marketplace and inventory migrations, create the two temporary input tables:

```sql
CREATE TABLE #FirstPartyVendor (VendorId int NOT NULL);
CREATE TABLE #FirstPartyListings (ProductId int NOT NULL, SellerSku nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL, Price decimal(18,3) NOT NULL);
-- Insert the reviewed vendor ID and reviewed product mappings here.
-- No example IDs or quantities are automatically inserted.
```

Run scripts/inventory/01-reconcile.sql on the restored legacy data. It now excludes marketplace order lines from legacy sales totals. Its totals are still diagnostics, not opening balances. Resolve orphan rows, old stock writers, pending orders, returns, and physical-count differences separately.

Then include scripts/marketplace/13-stage-first-party-listings.sql in the same session. The script only creates missing Pending listings. Exact retries do not duplicate listings; conflicts, missing products, inactive products and unapproved vendors are rejected. No original product, historical order, price on an existing listing, stock, or vendor approval is changed. No inventory balance is created. Existing exact listings are left unchanged regardless of status; review those separately before activation.

Do not enable marketplace checkout or approve newly staged listings on the basis of this script. Approved physical opening quantities and a legacy-writer freeze/cutover plan are separate mandatory gates. Reconcile open reservations and paid/unpaid orders before any opening stock posting. Authentication and mobile API changes remain outside the current development scope; legacy mobile writes must be isolated before shared-stock activation.

CI fixture: test-first-party-listings.sql uses synthetic data only, exercises repeat staging and rejection/rollback, and checks absence of stock movements.
