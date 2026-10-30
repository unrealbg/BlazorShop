# UI v2 — Shopping cart

Based on `master` at `27849e1a8a649b8ed30708c8959244c5c0684a50`, including the merged storefront foundation, category/product pages and listing pages.

## Presentation and design

The existing `/my-cart` page follows `docs/ui-v2/cart.html`, using the established storefront tokens, buttons, header and footer. The product panel and Order Summary sit side by side on desktop; the summary follows the items on smaller screens. Item counts represent quantities, with singular/plural wording.

Rows show the actual product image or missing-image placeholder, linked product name, available SKU/size/color, unit price, line total, quantity input and Remove action. Current EUR formatting is preserved. Clear Cart, Continue Shopping and Continue to Checkout retain their existing destinations and behavior. Empty, warning and malformed-cookie states use scoped styles and accessible status/alert regions. Quantity controls have explicit labels, unique IDs, contextual accessible names and a visible focus outline.

The prototype's stock claims, shipping/discount rows, payment-method guarantees and unsupported catalog route are omitted. The actual cart keeps its numeric quantity input, product-detail navigation and existing clear action. Displayed amounts are explicitly informational; the server revalidates products, variants, prices and stock during checkout.

## Compatibility

`CartPage.razor.cs`, `storefrontCommerce.js` and `StorefrontRoutes.cs` are unchanged. The cookie name `my-cart`, field names and ProductId/VariantId/SizeValue payload remain unchanged. All existing quantity/remove/clear DOM selectors and product/variant/name/size attributes are retained. The JavaScript still performs the same mutations, persistence, badge updates, reloads and toast notifications.

The route remains `/my-cart`; checkout still uses `StorefrontRoutes.Checkout`. Static SSR, authentication/session ownership, anonymous sign-in handoff, `noindex,nofollow`, the non-cacheable response headers and exclusion of canonical/structured data are preserved. No new render mode or endpoint is added.

**Intentional business-behavior changes: none.** There are no changes to API contracts, Domain, Application, database schema, migrations, security policy, inventory, checkout, payment or order processing.

## Changed files

- `BlazorShop.Presentation/BlazorShop.Storefront/Pages/CartPage.razor`: presentation and accessible controls.
- `BlazorShop.Presentation/BlazorShop.Storefront/wwwroot/css/ui-v2-storefront.css`: scoped cart layout, responsive and state styles.
- `BlazorShop.Tests/Presentation/Storefront/StorefrontCartFlowTests.cs`: current-price, variant, empty, missing-data, zero-stock, malformed-cookie and privacy checks.
- `BlazorShop.Tests/Presentation/Storefront/Support/StorefrontSeoAuditTestHost.cs`: test-only per-product/status responses.
- `BlazorShop.Tests/Presentation/Storefront/StorefrontMobileMarkupTests.cs`: checks the cart's scoped controls instead of its replaced utility classes.
- `BlazorShop.E2E/StorefrontCheckoutTests.cs`: functional cart scenario, responsive captures and explicit ProductId/VariantId/quantity cookie assertions before and after authentication handoff.
- `BlazorShop.E2E/E2EApplicationFixture.cs`: isolated second variant and actual image sample for cart testing.
- `scripts/verify-e2e-results.ps1`: retains the five existing mandatory scenarios and additionally requires the sixth cart scenario.
- This review document.

## Regression evidence

Eight additional HTTP cases cover empty cart, two variants, missing product, missing variant, zero product stock, API unavailability, partial catalog data and malformed JSON. The existing populated-cart test now deliberately supplies a stale cookie price and verifies the API price is displayed. Existing authentication/redirect and commerce-script checks remain.

The mandatory browser suite retains all five existing scenarios. The new cart scenario exercises empty cart; a simple product plus two variants of the same product; real and missing images; quantity increase/decrease; refresh persistence; removal of exactly one variant while preserving the other; simple-line removal; singular/plural counts; badge/subtotal updates; Clear Cart and cookie removal; existing toast messages; malformed-cookie and unavailable-item rendering; keyboard focus and control dimensions. The existing checkout scenario additionally checks exact ProductId, VariantId and quantity through anonymous login and authenticated checkout.

The local Release restore/build passed with warnings as errors and zero build warnings/errors. The unit/integration suite passed **708 tests**, with **10 existing opt-in smoke skips**. PostgreSQL 16 archive tests passed **11/11**. Runtime/full npm and transitive NuGet audits returned **zero vulnerabilities**. All **six browser scenarios passed twice consecutively**, including responsive and state captures. Final exact-head GitHub CI evidence is recorded in the PR description after validation; earlier commits are not final-head evidence.

## Screenshots

Each browser run uploads `ui-v2-cart/` with 21 captures:

- `cart-filled-{width}.png`: actual API-backed simple product and two variant rows.
- `cart-empty-{width}.png`, `cart-invalid-cookie-{width}.png`, `cart-unavailable-{width}.png`: functional states.
- `prototype-cart-{width}.png`: original reference at the matching width.
- `cart-keyboard-focus-320.png`: visible keyboard focus on the quantity input.

Widths are 320, 390, 768 and 1440 px. Compare panel layout, spacing, thumbnails, metadata wrapping, controls and summary placement at matching widths. Actual content, extra supported actions, unit/line prices, the inherited header/footer and omission of unsupported features deliberately differ from the static prototype. Fixtures exist only in the isolated test database; production uses real API data.

## Existing limitations and follow-up boundaries

- Unavailable rows remain editable/removable and do not disable the checkout link. Checkout continues to enforce the server's existing validation. No new checkout policy or client stock limit is introduced.
- A missing variant currently falls back to the product's base price; a missing product or failed lookup renders an unavailable row at zero. Consequently, the displayed subtotal may be incomplete during catalog failures. This is preserved existing behavior, made visible through warnings and the informational-price notice.
- The existing unavailable flag groups missing products/variants and zero stock under a common warning; it does not provide a precise per-cause status. Quantities above stock remain subject to server-side checkout validation.
- Malformed JSON is displayed as an empty cart with an error, without automatically rewriting the saved cookie. Inspection of the existing reader also shows that null entries in a syntactically valid array are not explicitly validated. Broader structural cookie validation needs a separately approved hardening change; this presentation update does not alter the reader.
- Zero-stock, partial-data and API-failure responses are covered by HTTP integration tests. Browser state captures exercise malformed JSON, missing products and missing variants against the isolated API.
- Image checks use an actual local WebP and a missing-image fallback. Broken nonempty remote image URLs and unusual image dimensions are outside this coverage.
- Chromium screenshots support human visual review. There is no pixel-diff gate, cross-browser/device certification or full accessibility audit. The ten opt-in live smoke tests remain skipped; no live payment or production smoke test is claimed.

The existing .NET 10 static SSR and native controls are retained in accordance with the official [render-mode](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0) and [routing](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing?view=aspnetcore-10.0) guidance.
