# UI v2 — Checkout, payment results and public information

Baseline: `0be62414ca7e8728d4fc621ddff8e4791d36a3bc`, including the Sucrase 3.35.1 remediation and zero-finding npm gates.

## Presentation

The authorized checkout route uses the prototype's light header, heading, bordered product panel, order summary, blue actions and responsive layout. The existing quantity/remove actions display actual catalog prices, variant details, images or a missing-image fallback. Subtotal and total reflect the existing cart mapper; no address form, delivery charge, discount, tax engine or unsupported payment choice is introduced.

Payment methods retain the existing API query and selection handler. Native checkout dialogs provide an inert background, Tab/Shift+Tab containment, Escape handling, visible focus and focus restoration. Close controls remain disabled while a payment request is processing. Existing operation feedback is also displayed inside the active dialog so errors remain accessible. Bank details come entirely from the typed checkout response.

Payment return screens display COD collection, expected bank transfer or Stripe awaiting server confirmation. Both `card` and the existing recovered-checkout `stripe` spelling select the card explanation. Query parameters are navigation context, never payment evidence. No order status, financial snapshot, paid badge or cart-availability promise is inferred from a URL. The cancel screen links to Orders and performs no mutations.

The public About, Customer Service, FAQ, Privacy, Terms and 404 screens use the shared storefront layout and scoped UI v2 styles. Native FAQ disclosures and legal section links work without JavaScript. Contact details use the existing `IStorefrontSeoSettingsProvider`; absent values are omitted and an explicit unavailable message replaces fictional contacts. FAQ visible text and JSON-LD use the same collection. Legal text remains general, with an explicit approval notice.

## Preserved contracts

`Cart.razor.cs`, `CartCookieParser`, `CheckoutCartLineMapper`, `ICheckoutAttemptStore`, commerce API/application/domain/infrastructure code, database schema and migrations are unchanged. Checkout uses the existing typed result, payment-kind switch, idempotency state, success/cancel URLs and cart cleanup. COD creates a confirmed order with payment pending; bank transfer reserves stock; Stripe remains authoritative through reconciliation and provider events.

The public `/checkout` redirect, login continuation, ProductId/VariantId/Quantity payloads, customer/admin policies, JWT/session validation, credential/refresh/logout handlers and demo isolation are unchanged. No provider transaction, new bank-transfer route, refund or payment-retry action is added. Checkout and payment pages include noindex metadata; existing private redirect/cache protections and public canonical, structured data, sitemap, robots and HTTP 404 behavior remain.

## Verification

All 21 existing mandatory browser scenarios remain. Six additional mandatory scenarios cover responsive checkout/public/result pages; actual COD and bank-transfer API submissions and double-submit guards; checkout loading/empty/invalid/unavailable/API failure/retry; payment URL spoofing with database snapshots; public SSR/SEO/native keyboard controls with JavaScript disabled; and a safe synthetic Stripe redirect presentation boundary. The strict verifier requires all 27 passing in each consecutive run.

COD/bank requests use synthetic customers and real isolated API/database operations, validating totals, order/payment/fulfillment separation and reservation state. Bank settings are explicitly synthetic in the browser fixture and no email is sent. The double-submit test delays the real response, queues two clicks, verifies one request and one persisted order, and checks the idempotency header. Public checkout handoff checks both anonymous and authenticated navigation, retaining the exact variant and quantity.

Payment return tests compare orders, payment transactions, reservations and product/variant stock before and after forged success/cancel visits and reloads. These screens make no checkout POST and leave the cart cookie intact. The Stripe browser response is intercepted only in the test runner; it does not contact Stripe, create a payment or bypass production validation. Existing backend Stripe/reconciliation tests remain responsible for provider lifecycle rules.

Five HTTP integration cases additionally verify configured/missing/encoded contacts, exact FAQ HTML/JSON-LD agreement and explicit legal approval notices. Existing route SEO and 404 audits remain. CI verifies the published checkout CSS/JS and storefront UI CSS against their sources, in addition to existing context-specific generated CSS checks and all containers.

Exact final-head CI, Code Analysis, audit JSON, strict TRX reports and screenshot artifact links are recorded in the PR description after verification. Earlier-head runs are baseline evidence only. Local Release build passes with warnings as errors; the non-container suite passes 745 tests with the ten existing opt-in live smoke skips.

## Visual evidence

The initial PR commit `7be07d23462b5b70024f997aaa342a308a6e8242` captures the unchanged application at 320, 390, 768 and 1440 px. The final browser artifacts contain `ui-v2-checkout-public/`, with matching checkout, payment-method, result and public-page captures, plus bank instructions, processing, empty/loading/error/unavailable and synthetic Stripe return states. `no-js/` contains the public pages with scripting disabled. Compare matching widths and state names; financial values and identities are synthetic fixture data. Product image rendering uses actual local image assets and a missing-image state.

## Limitations and production content decisions

- Chromium coverage and human screenshot review do not constitute cross-browser or a full accessibility audit. There is no live Stripe, email or production test; the ten opt-in smoke tests remain disabled. External provider navigation/reconciliation is covered by existing backend doubles rather than a real payment.
- Existing bank instruction dismissal/recovery and cart cleanup behavior is preserved; the page adds no persistent bank-instruction retrieval feature.
- The checkout contract has no trustworthy address source. No shipping/billing fields or delivery promises are fabricated. Cart totals are informational until server validation.
- The owner must supply and approve the merchant's identity and customer-service contact details through the existing settings. Configuration is not proof that a contact value is legally verified.
- Privacy needs merchant/legal decisions for controller identity, actual data categories and purposes, applicable legal bases, recipients/processors, international transfers, retention, cookies/analytics and rights/contact procedures. The existing general text is not an approved privacy policy.
- Terms need merchant/legal decisions for jurisdiction, prices/taxes, order acceptance, delivery, cancellation, returns, refunds, warranties and dispute handling. Existing text is not approved merchant terms and no new promise or commerce capability is implied.

The implementation follows official [.NET 10 component guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/?view=aspnetcore-10.0); it retains the application's WebAssembly and static SSR models.
