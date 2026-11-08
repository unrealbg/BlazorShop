\set ON_ERROR_STOP on
BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '15s';
SET LOCAL lock_timeout = '2s';
SET LOCAL search_path = pg_catalog, public;

-- Aggregate-only output. The operator must retain even these results in the approved environment.
SELECT 'transaction_read_only', current_setting('transaction_read_only');
SELECT to_regclass('public."LegacyCheckoutOrderItemsArchive"') IS NOT NULL AS archive,
       to_regclass('public."CheckoutOrderItems"') IS NOT NULL AS legacy,
       to_regclass('public."PaymentTransactions"') IS NOT NULL AS payment,
       to_regclass('public."PaymentProviderEvents"') IS NOT NULL AS events,
       EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'Orders' AND column_name = 'PaymentStatus') AS lifecycle,
       EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'Orders' AND column_name = 'Currency') AS currency
\gset
SELECT 'migration.count', count(*)::text FROM "__EFMigrationsHistory"
UNION ALL SELECT 'migration.ids', string_agg("MigrationId", ',' ORDER BY "MigrationId" COLLATE "C") FROM "__EFMigrationsHistory"
UNION ALL SELECT 'identity.users', count(*)::text FROM "AspNetUsers"
UNION ALL SELECT 'identity.roles', count(*)::text FROM "AspNetRoles"
UNION ALL SELECT 'identity.assignments', count(*)::text FROM "AspNetUserRoles"
UNION ALL SELECT 'identity.admin_users', count(*)::text FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" WHERE r."NormalizedName" = 'ADMIN'
UNION ALL SELECT 'identity.orphan_assignments', count(*)::text FROM "AspNetUserRoles" ur LEFT JOIN "AspNetUsers" u ON u."Id" = ur."UserId" LEFT JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" WHERE u."Id" IS NULL OR r."Id" IS NULL
UNION ALL SELECT 'inventory.products', count(*)::text FROM "Products"
UNION ALL SELECT 'inventory.variants', count(*)::text FROM "ProductVariants"
UNION ALL SELECT 'inventory.product_quantity_sum', coalesce(sum("Quantity"), 0)::text FROM "Products"
UNION ALL SELECT 'inventory.variant_stock_sum', coalesce(sum("Stock"), 0)::text FROM "ProductVariants"
UNION ALL SELECT 'inventory.negative_products', count(*)::text FROM "Products" WHERE "Quantity" < 0
UNION ALL SELECT 'inventory.negative_variants', count(*)::text FROM "ProductVariants" WHERE "Stock" < 0
UNION ALL SELECT 'inventory.nonzero_variant_parents', count(*)::text FROM "Products" p WHERE p."Quantity" <> 0 AND EXISTS (SELECT 1 FROM "ProductVariants" v WHERE v."ProductId" = p."Id")
UNION ALL SELECT 'money.negative_catalog_prices', (SELECT count(*) FROM "Products" WHERE "Price" < 0)::text
UNION ALL SELECT 'money.negative_variant_prices', (SELECT count(*) FROM "ProductVariants" WHERE "Price" < 0)::text
UNION ALL SELECT 'orders.count', count(*)::text FROM "Orders"
UNION ALL SELECT 'orders.negative_totals', count(*)::text FROM "Orders" WHERE "TotalAmount" < 0
UNION ALL SELECT 'orders.nonfinite_totals', count(*)::text FROM "Orders" WHERE "TotalAmount"::text IN ('NaN', 'Infinity', '-Infinity')
UNION ALL SELECT 'orders.totals_outside_decimal_range', count(*)::text FROM "Orders" WHERE abs("TotalAmount") > 79228162514264337593543950335
UNION ALL SELECT 'lines.count', count(*)::text FROM "OrderLines"
UNION ALL SELECT 'lines.invalid_quantity_or_price', count(*)::text FROM "OrderLines" WHERE "Quantity" <= 0 OR "UnitPrice" < 0 OR "UnitPrice"::text IN ('NaN', 'Infinity', '-Infinity')
UNION ALL SELECT 'lines.extended_price_outside_decimal_range', count(*)::text FROM "OrderLines" WHERE abs("UnitPrice") > 79228162514264337593543950335 / greatest(1, "Quantity")
UNION ALL SELECT 'lines.catalog_fallback', count(*)::text FROM "OrderLines" l LEFT JOIN "Products" p ON p."Id" = l."ProductId" WHERE p."Id" IS NULL OR p."Name" IS NULL
UNION ALL SELECT 'payment.methods', count(*)::text FROM "PaymentMethods";

\if :lifecycle
SELECT 'orders.legacy_paid_labels', count(*)::text FROM "Orders" WHERE "LegacyStatus" = 'Paid'
UNION ALL SELECT 'orders.fulfillment_review', count(*)::text FROM "Orders" WHERE "FulfillmentStatus" = 'ReviewRequired'
UNION ALL SELECT 'orders.payment_pending', count(*)::text FROM "Orders" WHERE "PaymentStatus" = 'Pending';
\else
SELECT 'orders.legacy_paid_labels', count(*)::text FROM "Orders" WHERE "Status" = 'Paid'
UNION ALL SELECT 'orders.legacy_status_too_long', count(*)::text FROM "Orders" WHERE length("Status") > 64 OR length("ShippingStatus") > 64
UNION ALL SELECT 'orders.legacy_status_unmapped', count(*)::text FROM "Orders" WHERE "Status" IS NULL OR "Status" NOT IN ('Pending', 'Paid', 'Cancelled', 'PaymentFailed')
UNION ALL SELECT 'orders.legacy_fulfillment_unmapped', count(*)::text FROM "Orders" WHERE lower(btrim(coalesce("ShippingStatus", ''))) NOT IN ('pendingshipment', 'shipped', 'intransit', 'outfordelivery', 'delivered');
\endif

\if :currency
SELECT 'orders.currency_non_eur', count(*)::text FROM "Orders" WHERE "Currency" IS DISTINCT FROM 'EUR';
\else
SELECT 'orders.currency_non_eur', 'not observable in this schema';
\endif
\if :legacy
SELECT 'legacy.checkout_rows', count(*)::text FROM "CheckoutOrderItems";
\else
SELECT 'legacy.checkout_rows', 'table absent';
\endif
\if :archive
SELECT 'archive.checkout_rows', count(*)::text FROM "LegacyCheckoutOrderItemsArchive";
\else
SELECT 'archive.checkout_rows', 'table absent';
\endif
\if :payment
SELECT 'payment.transactions', count(*)::text FROM "PaymentTransactions"
UNION ALL SELECT 'payment.pending_transactions', count(*)::text FROM "PaymentTransactions" WHERE "Status" = 'Pending';
\else
SELECT 'payment.transactions', 'table absent';
\endif
\if :events
SELECT 'payment.events', count(*)::text FROM "PaymentProviderEvents";
\else
SELECT 'payment.events', 'table absent';
\endif

ROLLBACK;
