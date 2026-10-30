# Cart cookie validation and recovery

Base: `master` at `c044554b9e31edc4c68fcc077f4fe3b05d377be9`.

## Root cause

The SSR cart and WebAssembly checkout deserialized the cookie into `List<ProcessCart>`. A valid JSON array can contain null elements even when the C# element annotation is non-nullable. The SSR reader dereferenced each element in its filter; the checkout dereferenced it while collecting product IDs. Both `[null]` and a valid item mixed with null reproduced HTTP 500 in the SSR integration host before the fix.

Validation also differed across clients: JavaScript accepted loosely parsed IDs/quantities, the checkout retained invalid entries, and the SSR filtered only some of them. `decodeURIComponent` could throw on malformed encoding. `Enumerable.Sum(int)` could overflow across distinct valid rows, while decimal multiplication/summation could fail during rendering.

## Strategy and existing contract

`CartCookieParser` is a small pure helper in Web.Shared, used by the SSR cart, WebAssembly checkout and workspace header. It is not a service abstraction and changes no API or Domain contract. JavaScript implements the same identity/quantity normalization, tested against the same 28-case input corpus in a real browser.

- Missing/blank cookie and an empty array are empty carts. Invalid JSON or a non-array root recovers to an empty cart with the existing read-error message.
- Null/scalar entries, invalid/empty GUIDs and quantities outside positive `Int32` are discarded, with a warning when other valid items remain. No stock limit or arbitrary quantity maximum is introduced: the range is the existing `ProcessCart.Quantity` and server checkout contract.
- PascalCase fields and the camelCase aliases already accepted by the storefront script are supported. GUIDs normalize to canonical identity. Integral JSON numbers such as `2.0` normalize to the same integer value across JavaScript/.NET; numeric strings are not quantities.
- Invalid optional SizeValue/UnitPrice metadata is cleared with a warning while retaining valid identity and quantity. Valid metadata is retained. Client prices are never used as authoritative display or order prices.
- Duplicate ProductId/VariantId groups are summed with checked `Int32` arithmetic, matching the existing `CheckoutIntentCanonicalizer`. Different variants remain separate. A group whose sum cannot be represented is rejected in full, including later entries for that key; unrelated valid groups remain. No clamping or partial quantity is substituted.
- GET/read operations never write or delete the cart cookie. Existing explicit add/update/remove/clear operations persist normalized items. An add/update outside `Int32` is rejected with feedback; the cookie is preserved. Quantity zero retains the existing explicit-remove action.
- Malformed URL encoding is read safely for cart cookies. Other cookie decoding behavior in the shared storage module is unchanged.
- SSR item counts use `long`; the storefront badge retains its existing `99+` display cap. The workspace header retains its existing distinct-line count, now based on the same validated rows.
- Unrepresentable API-derived line/subtotals display **Unavailable** with a warning. Quantities are retained. No new checkout/payment policy is introduced; the existing server already rejects unrepresentable order amounts.

The cookie remains `my-cart`, with ProductId, VariantId, Quantity, SizeValue and UnitPrice fields. Product/variant prices and metadata still come from the API. Routes, Static SSR, noindex/no-store behavior, authentication/session handling, checkout intent/idempotency, inventory and payment processing remain unchanged. ProcessCart, Domain, schema, migrations and CheckoutIntentCanonicalizer are unmodified.

## Before and after

| Input/state | Before | After |
| --- | --- | --- |
| `[null]`, valid item plus null | Reproduced SSR HTTP 500; checkout dereference failure | HTTP 200, warning, safe valid subset |
| Malformed JSON or unexpected root | SSR error recovery, but checkout could throw | Both readers recover with a message |
| Empty/invalid ID or nonpositive/wrong-type quantity | Different rows/counts across readers | Consistent filtering; warning; safe entries retained |
| Invalid optional metadata | Could reject the entire cookie | Clear bad metadata, retain valid identity/quantity |
| Valid duplicate identities | Multiple UI rows; server already sums them | One row with the same canonical quantity/checkout fingerprint |
| Duplicate sum above Int32 | Could overflow before server rejection | Reject that entire identity group, retain unrelated groups |
| Distinct rows with aggregate quantity above Int32 | SSR count could overflow | Exact `long` count; existing badge cap |
| API-derived decimal overflow | Render exception | Unavailable amount plus warning; no client-price substitution |
| Malformed percent encoding | JavaScript URIError | Safe parser recovery; no automatic cookie write |
| Missing product/API connection interruption | Existing unavailable/error states | Preserved, including mixed invalid-cookie inputs |
| Valid cart and checkout handoff | Working identity/quantity/pricing flow | Preserved, with added browser assertions |

## Tests and evidence

The shared corpus covers 28 cases: null/mixed arrays, syntax/root errors, scalar entries, empty/invalid product and variant identifiers, zero/negative/fractional/string/out-of-range quantities, extreme numeric values, invalid optional metadata, duplicate aggregation/overflow, large distinct counts, valid/empty carts, camelCase aliases and integral numeric representations. Parser, SSR HTTP and actual WebAssembly cart loading tests consume the corpus. HTTP tests verify 200, noindex/no-store and no Set-Cookie. Additional tests cover safe display of unrepresentable API prices and interrupted/missing catalog data. A fingerprint regression proves duplicate normalization preserves the existing server checkout intent.

All six existing mandatory E2E scenarios remain. A seventh is necessary to verify the actual JavaScript behavior: the corpus runs through the SSR UI and badge, malformed URL encoding does not throw, Int32 add overflow preserves the cookie, and mixed data survives anonymous login/authenticated checkout without a GET rewrite. An explicit checkout quantity edit persists the safe cart, which remains compatible when returning to SSR. No order/payment is submitted.

Baseline reproduction: two new HTTP tests failed with InternalServerError before implementation. Local Release build and focused regressions are recorded in the final PR validation. Local container-backed checks depend on the test runtime being available; the completed exact-head GitHub CI results are the final evidence for PostgreSQL, containers and consecutive browser runs. The PR description records the exact SHA, full test counts, audit results and direct CI/artifact links after completion.

## Changed files

- `BlazorShop.Presentation/BlazorShop.Web.Shared/CookieStorage/CartCookieParser.cs`
- `BlazorShop.Presentation/BlazorShop.Storefront/Pages/CartPage.razor.cs` and `CartPage.razor`
- `BlazorShop.Presentation/BlazorShop.Storefront/wwwroot/js/storefrontCommerce.js`
- `BlazorShop.Presentation/BlazorShop.Web/Pages/Payments/Cart.razor.cs`, `Cart.razor` and `CheckoutCartLine.cs`
- `BlazorShop.Presentation/BlazorShop.Web/Components/Header/HeaderBoxComponent.razor.cs`
- `BlazorShop.Presentation/BlazorShop.Web/wwwroot/js/cartBadge.js` and `cookieStorage.js`
- `BlazorShop.Tests/Presentation/Payments/CartCookieParserTests.cs` and `CartCookieCheckoutTests.cs`
- `BlazorShop.Tests/Presentation/Storefront/StorefrontCartFlowTests.cs` and `Support/StorefrontSeoAuditTestHost.cs`
- `BlazorShop.E2E/StorefrontCheckoutTests.cs`
- `tests/fixtures/cart-cookie-cases.json`
- `scripts/verify-e2e-results.ps1`
- This report.

## Limits and behavior assessment

This hardening changes how invalid cookie records and duplicate representations are displayed and persisted after an explicit action. It does not change valid order meaning, introduce inventory limits or alter payment/idempotency policy. Duplicate overflow removes the whole invalid group rather than guessing a smaller requested quantity; a visible warning requires the user to review the remaining cart.

Unavailable product/variant policy is preserved: SSR still shows unavailable rows, while authenticated checkout retains its existing unavailable-line checks. Cookie validation does not establish stock or price authority. Decimal display recovery is technical error handling; the server remains authoritative and its existing amount validation is unchanged.

JavaScript and .NET need separate implementations because storefront behavior runs without the WebAssembly runtime. The common corpus and mandatory browser regression guard their identity/quantity parity. Optional decimal metadata has different numeric representation in JavaScript, is never trusted for prices and is not an order input. This change targets the SSR cart, checkout and their badges; legacy workspace discovery components are not redesigned. Browser coverage remains Chromium; no production smoke test or real payment is claimed.
