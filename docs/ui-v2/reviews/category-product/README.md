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

## Validation and visual evidence

**Validated implementation head `f724d5fce25ad5beb7a0fb676cdc3760467c7d81`:**
- [Complete CI run 40956077247](https://github.com/unrealbg/BlazorShop/actions/runs/40956077247) — PASS. Release restore/build with warnings as errors, **694 passed / 10 opt-in live smoke skipped / zero failed**, PostgreSQL 16 archive regression, PostgreSQL 17 integration tests, Compose config, all three container builds and published storefront assets.
- [Code Analysis run 40956077257](https://github.com/unrealbg/BlazorShop/actions/runs/40956077257) — PASS.
- [Browser E2E TRX and screenshots](https://github.com/unrealbg/BlazorShop/actions/runs/40956077247/artifacts/12932784319): **4/4 passed in each of two consecutive runs**. Both TRX files were inspected: no skipped/failed E2E scenarios. The route test covers 320/390/768/1440px category, simple product, and variant product captures, real API-backed seed data, missing-image state, 404 status, and no horizontal overflow.
- [npm audit JSON](https://github.com/unrealbg/BlazorShop/actions/runs/40956077247/artifacts/12933687145): runtime and full tree both report **0 vulnerabilities**.
- [NuGet audit JSON](https://github.com/unrealbg/BlazorShop/actions/runs/40956077247/artifacts/12933082377): **11 projects, 0 vulnerable packages**.

The screenshots from both runs were reviewed. The desktop category intro and two-column product layout, missing-image fallback, mobile stacking and purchase controls match the approved presentation direction. The actual fixture category contains two products, so no fake 24-product cards, stock labels or promotional claims appear. Mobile 320px related-product cards remain two-column under the #110 shared grid, with text wrapping but no page overflow. This is a visual difference from the single-card prototype mock, not a commerce regression.

**Final-head rule:** The above results validate `f724d5f...`, before a one-line singular-option copy correction and this documentation update. The [PR #114 final validation record](https://github.com/unrealbg/BlazorShop/pull/114) must link the *new* successful CI/Code Analysis runs on its final head before marking Ready for Review. Do not use these historical runs as proof for a different SHA. The agent environment has no local .NET SDK or Docker; GitHub Actions supplies executable verification.

## Known boundaries

Only Chromium E2E is automated in the current pipeline; Firefox/WebKit, physical devices, a full accessibility audit, production API smoke tests, and VPS proxy/cookie/domain checks are separate release gates. No production data, real payments or deployment are involved. The category endpoint's existing product order and the product page's current single image are authoritative; the prototype's mock sort, thumbnail gallery, rating and stock labels are intentionally not implemented.

**Review checkpoint:** Keep PR #114 Draft until CI, screenshots and evidence are verified; then mark Ready for Review and stop. No merge or VPS deployment is authorized by this PR.
