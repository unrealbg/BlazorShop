# PR A — Category and product detail (Issue #112)

Implementation: [PR #114](https://github.com/unrealbg/BlazorShop/pull/114). Baseline: merged [PR #110](https://github.com/unrealbg/BlazorShop/pull/110), `master` at `fdc4fa56823099cb773a6a95f84d1c1e63102653`.

## Scope and design references

This is the **presentation-only** category/product slice of [the UI v2 roadmap](https://github.com/unrealbg/BlazorShop/issues/113). It follows `docs/ui-v2/category.html`, `product.html`, `components.html`, `styles.css`, `polish.css` and `consistency.css`, while reusing the SSR shell, product cards, and live API data from #110.

- `Pages/CategoryPage.razor`: compact breadcrumb, pale blue category introduction, actual name/meta-description/count, real new-arrival/deals navigation, live product grid and category SEO content below the grid. No prototype-only sort control (the current category endpoint does not supply sorting).
- `Pages/ProductPage.razor`: responsive gallery and truthful missing-image state, actual product/category/price/description, supported variant select and price metadata, preserved Add to Cart / View Cart JavaScript hooks, real related products, SEO content and customer-service link. No invented SKU, color, stock badge, review rating, discount or quantity selector.
- `wwwroot/css/ui-v2-storefront.css`: page-scoped typography, color, spacing, controls, gallery and responsive layout at <=980/640px. The stylesheet remains versioned by the existing `App.razor` pipeline.
- `BlazorShop.Tests/Presentation/Storefront/StorefrontMobileMarkupTests.cs` and `StorefrontCategoryProductV2MarkupTests.cs`: responsive markup, 404/503 and SEO contract guards, variant/cart attributes, focus styles.
- `BlazorShop.E2E/StorefrontVisualTests.cs`: live database-backed category and simple/variant product screenshots at **320/390/768/1440px**, visible purchase/variant controls, missing-image state, no horizontal overflow, and missing category/product 404 assertions. Existing checkout E2E still exercises cart persistence, variants, anonymous login handoff and checkout.

## Contract preservation

Both page components retain their original `@code` blocks (API lookups, route parameters, SEO composers, structured data, 404/503 response headers, related-product query, cart price/variant metadata) unchanged. No render-mode changes, API/domain/database/migration/security/checkout/payment edits, new features, or production operations.

The existing `#purchase`, `#product-variant-select`, `#product-cart-feedback`, `data-storefront-add-to-cart`, `data-storefront-variant-select`, `data-product-*`, `data-unit-price`, `data-size-value`, `data-feedback-target` and `data-variant-select` contracts are preserved.

## Validation status

**Pending exact-head GitHub CI and Code Analysis.** The agent environment does not include a local .NET SDK or Docker; no local build/test claims are made. GitHub Actions is the authoritative validation for the PR head. Before Ready for Review, confirm all of the following from actual run artifacts:

- Release restore/build with warnings as errors, unit/integration tests, PostgreSQL 16 archive + PostgreSQL 17 regressions.
- NuGet transitive audit and npm full-tree/runtime audits; container builds and Compose verification.
- Four mandatory Playwright scenarios twice consecutively, including the extended responsive captures and 404 checks.
- Code Analysis and all required PR checks green on the same final head.
- Review actual screenshot artifacts against `docs/ui-v2/category.html` and `product.html`. Capture differences and residual risks before approval.

## Known boundaries

Only Chromium E2E is automated in the current pipeline; Firefox/WebKit, physical devices, a full accessibility audit, production API smoke tests, and VPS proxy/cookie/domain checks are separate release gates. No production data, real payments or deployment are involved. The category endpoint's existing product order and the product page's current single image are authoritative; the prototype's mock sort, thumbnail gallery, rating and stock labels are intentionally not implemented.

**Review checkpoint:** Keep PR #114 Draft until CI, screenshots and evidence are verified; then mark Ready for Review and stop. No merge or VPS deployment is authorized by this PR.
