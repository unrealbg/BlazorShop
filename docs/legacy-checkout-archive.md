# Legacy checkout-history archive

Issue #95 removes `CheckoutOrderItems` as an active persistence and order-history model. `Orders` and `OrderLines`, including their purchase-time customer, product, variant, monetary, payment, inventory, and lifecycle state, are the only runtime order-history source.

## Audit and final disposition

| Legacy use | Disposition |
| --- | --- |
| `OrderItem`, `ICart`, `CartRepository`, application `ICartService`/`CartService` | Removed from the runtime domain, application, persistence, and dependency-injection graph. |
| `CreateOrderItem`, `GetOrderItem`, and their mapping | Removed. |
| `POST /api/Cart/save-checkout` | Removed without redirecting it to the authoritative checkout orchestrator. The incomplete legacy request cannot create an order, reservation, payment, or history row. |
| `GET /api/Cart/order-items`, `GET /api/Cart/user/order-items` | Removed. Customer and admin history continue through `GET /api/Cart/user/orders` and `GET /api/Cart/orders`. |
| Browser `CartService`, cart cookie, and checkout attempt storage | Retained because they serve the current authoritative checkout flow, not legacy history persistence. |
| Earlier EF migrations and their designer metadata | Retained unchanged as immutable migration history. |
| `DatabaseMigrationBootstrapper` initial-schema table metadata | Retained only to recognize the supported pre-EF-history initial schema safely. It is not a current runtime dependency. |
| Physical `CheckoutOrderItems` data | Atomically renamed to `LegacyCheckoutOrderItemsArchive`; it has no `DbSet`, repository, service, route, or business behavior. |

## Why archived rows are not converted into orders

Each legacy row contains only `Id`, `ProductId`, `Quantity`, nullable `UserId`, and `CreatedOn`. It does not establish order boundaries, purchase-time price or currency, variant identity, payment evidence, fulfillment state, or inventory ownership. Grouping rows by nearby timestamps or current catalog data would manufacture commercial facts. The migration therefore creates no `Order`, `OrderLine`, `PaymentTransaction`, provider event, idempotency record, or inventory reservation, and changes no stock.

## Migration guarantees

PostgreSQL renames the existing table in place inside the EF migration transaction. This is an atomic metadata operation: there is no copy window, conflict resolution, filtering, coercion, or deduplication. The same physical rows and primary key remain, preserving every original ID and value, including null users, orphan product IDs, unusual quantities, timestamps, and identical-content rows with different IDs.

The runtime EF model deliberately does not map either the old table or the archive. The generated idempotent migration script uses EF migration history to apply the rename once. The `Down` migration renames the same table back to `CheckoutOrderItems`, preserving its rows; it is a data-preserving schema rollback, not rolling-version compatibility.

## Operator inspection and export

The archive is operational data, not an application query source. Inspect it read-only after the migration:

```sql
SELECT "Id", "ProductId", "Quantity", "UserId", "CreatedOn"
FROM "LegacyCheckoutOrderItemsArchive"
ORDER BY "Id";
```

Export it from `psql` to an operator-controlled path with client-side `\copy`:

```text
\copy (SELECT "Id", "ProductId", "Quantity", "UserId", "CreatedOn" FROM "LegacyCheckoutOrderItemsArchive" ORDER BY "Id") TO 'legacy-checkout-order-items.csv' WITH (FORMAT csv, HEADER true)
```

Record a row count and retain a protected database backup before cutover. Do not edit, import, or reinterpret archive rows as orders without a separately reviewed recovery process.

## Deployment and rollback

This change is not compatible with a rolling mix of old and new application binaries. An old binary still writes `CheckoutOrderItems`; after the rename that write fails.

Cutover procedure:

1. Take and verify a database backup.
2. Stop or drain every old API instance and wait for in-flight checkout/history writes to finish.
3. Apply the migration, which atomically renames the table.
4. Deploy only binaries containing the #95 runtime cleanup.
5. Verify migration history, archive row count and representative field values; then verify authoritative checkout and customer/admin order routes.

For rollback, first stop the new binaries, run the migration `Down` to rename the archive back with its data intact, and deploy the matching old binaries. Do not run old and new writers concurrently. Any operator changes made directly to the archive after cutover are outside the automated rollback guarantee.
