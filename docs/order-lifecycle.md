# Order, payment, and fulfillment lifecycle

Issue #94 separates three facts that were previously stored in `Orders.Status` and `Orders.ShippingStatus`. `PaymentTransaction.Status` remains authoritative for Stripe; `Order.PaymentStatus` is its atomically maintained order projection. Cash on delivery and bank transfer do not create synthetic Stripe transactions.

## State meanings

| Dimension | Values | Meaning |
| --- | --- | --- |
| Order | `Pending`, `Confirmed`, `Completed`, `Cancelled` | `Confirmed` means the shop accepted the order. `Completed` means a paid order was delivered. Neither state is a synonym for payment or inventory consumption. |
| Payment | `Pending`, `Paid`, `Failed`, `Cancelled` | The commercial payment state. Stripe changes it only through the coordinated payment transition. Delivery never changes it. |
| Fulfillment | `NotStarted`, `Shipped`, `InTransit`, `OutForDelivery`, `Delivered` | Physical fulfillment only. The supported forward paths are `NotStarted -> Shipped -> InTransit -> OutForDelivery -> Delivered` and `InTransit -> Delivered`. |
| Inventory reservation | `Reserved`, `Consumed`, `Released` | Inventory ownership. Consumption is not proof of payment: COD consumes inventory while payment remains pending. |

## Checkout and payment transitions

| Event | Order | Payment | Fulfillment | Inventory |
| --- | --- | --- | --- | --- |
| COD checkout commits | `Confirmed` | `Pending` | `NotStarted` | `Consumed` |
| Bank-transfer checkout commits | `Confirmed` | `Pending` | `NotStarted` | `Reserved` |
| Stripe Session is initialized | `Pending` | `Pending` | `NotStarted` | `Reserved` |
| Valid Stripe paid reconciliation | `Confirmed` | `Paid` | unchanged | `Consumed` |
| Valid Stripe failure/cancellation | `Cancelled` | `Failed`/`Cancelled` | unchanged | `Released` |
| Paid order is delivered | `Completed` | unchanged (`Paid`) | `Delivered` | unchanged |
| COD order is delivered | `Confirmed` | unchanged (`Pending`) | `Delivered` | unchanged (`Consumed`) |

Unpaid Stripe and bank-transfer orders are not eligible for fulfillment. Confirmed COD orders are eligible because collection happens at delivery. Repeating an already-applied fulfillment transition is idempotent and does not rewrite timestamps. Backward transitions, skipped transitions other than `InTransit -> Delivered`, invalid enum values, and shipping a cancelled/completed order are rejected as conflicts or validation errors. There is no generic payment-status setter or generic order cancellation path.

## Purchase-time snapshots

New orders store the authenticated customer's name and email, currency, line snapshots, and `SubtotalAmount`, `DiscountAmount`, `ShippingAmount`, `TaxAmount`, and `TotalAmount`. Current pricing has no discount, shipping, or tax engine, so those three components are zero and subtotal equals total. Their introduction does not change payable-total or minor-unit calculations.

The checkout contract currently has no trustworthy address source. `ShippingAddressSnapshot` and `BillingAddressSnapshot` therefore remain null rather than inventing an address. Legacy orders also keep null customer snapshots; customer/admin history must display that absence and must not substitute the customer's current profile as historical truth.

## Legacy migration mapping

The migration preserves the old strings in read-only `LegacyStatus` and `LegacyShippingStatus` columns for diagnostics. They are not writable lifecycle authorities.

Payment mapping is conservative: a `Paid` payment transaction is required to backfill `PaymentStatus = Paid`. Transaction failure/cancellation and the old explicit `PaymentFailed`/`Cancelled` values map to the matching terminal payment state. A legacy `Paid` order string without provider evidence remains `PaymentStatus = Pending`. Stripe transactions identify Stripe; durable checkout records identify COD or bank transfer; otherwise payment method is `Unknown`.

Known fulfillment values are retained, including `InTransit` and `OutForDelivery`; `PendingShipment`, legacy `Cancelled`, and unknown strings map to `NotStarted`. Paid-and-delivered orders map to `Completed`; paid orders and reliably identified COD/bank orders map to `Confirmed`; failed/cancelled orders map to `Cancelled`; unresolved records remain `Pending`. Existing totals, currency, provider identity, reservations, timestamps, and stock are not changed. Existing totals are copied to subtotal and the new zero-value components preserve the total equation.

## Deployment and compatibility

Deploy the database migration before starting the new application version, with checkout/webhook traffic quiesced during the cutover. This change is not rolling-version compatible: the previous application writes the removed legacy column names while the new application writes the split columns. Take a database backup and review legacy rows whose payment method resolves to `Unknown` before deployment. The historical `CheckoutOrderItems` table and routes are unchanged.

The order API contract now exposes `orderStatus`, `paymentStatus`, `paymentMethod`, and `fulfillmentStatus` instead of the ambiguous `status` and `shippingStatus` fields. The existing routes remain unchanged; the shipping-status request body now uses `fulfillmentStatus`. Old persisted checkout outcome JSON is unchanged and remains replayable.

Refunds, returns, payment authorization, manual bank-payment confirmation, COD collection recording, an outbox, and inventory reservation expiry are intentionally outside #94.
