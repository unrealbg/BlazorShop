# UI v2 — Category and product details

Implementation: [PR #114](https://github.com/unrealbg/BlazorShop/pull/114), based on merged [PR #110](https://github.com/unrealbg/BlazorShop/pull/110) and `master` at `fdc4fa56823099cb773a6a95f84d1c1e63102653`.

## Scope and design references

The presentation follows `docs/ui-v2/category.html`, `product.html`, `components.html`, `styles.css`, `polish.css` and `consistency.css`, reusing the existing SSR shell, product cards and live API data.

- `Pages/CategoryPage.razor`: breadcrumbs, category introduction, actual name/description/product count, existing New Releases/deals navigation, published product grid and category SEO content. The existing endpoint's product order is preserved; no unsupported sorting control is introduced.
- `Pages/ProductPage.razor`: responsive image area and missing-image state, actual product/category/price/description, existing variant selector and purchase controls, related products, SEO content and support link. No fabricated ratings, discounts, stock labels, image thumbnails or quantity selector.
- `wwwroot/css/ui-v2-storefront.css`: scoped page typography, colors, spacing, gallery, controls and responsive layouts.
- `StorefrontCategoryProductV2MarkupTests.cs` and `StorefrontMobileMarkupTests.cs`: markup, SEO/status and variant/cart contract guards.
- `BlazorShop.E2E/StorefrontVisualTests.cs`: category and simple/variant product captures at 320/390/768/1440px, visible purchase controls, missing-image state, overflow and missing-route HTTP 404 assertions. Existing scenarios retain cart persistence, variant, sign-in and checkout coverage.

## Contract preservation

The components retain the existing API lookups, route parameters, SEO/structured-data composers, HTTP 404/503 behavior, related-product query and cart metadata. No changes to render modes, public API contracts, domain rules, schema, migrations, authentication, payment, inventory or checkout logic.

The existing `#purchase`, `#product-variant-select`, `#product-cart-feedback`, `data-storefront-add-to-cart`, `data-storefront-variant-select`, `data-product-*`, `data-unit-price`, `data-size-value`, `data-feedback-target` and `data-variant-select` contracts remain in place.

## Revision and validation evidence

Initially reviewed implementation: `f724d5fce25ad5beb7a0fb676cdc3760467c7d81`, with successful [CI](https://github.com/unrealbg/BlazorShop/actions/runs/40956077247) and [Code Analysis](https://github.com/unrealbg/BlazorShop/actions/runs/40956077257).

Latest verified revision before this documentation-only closure update: **`59e85a7e9ffe9f3555bd45e91edbb3bf168fd1ce`**. Relative to the initially reviewed implementation, it contains the existing singular-option copy correction described below and documentation changes. Both workflows passed at that exact revision:

- [Complete CI run 40956916606](https://github.com/unrealbg/BlazorShop/actions/runs/40956916606).
- [Code Analysis run 40956916574](https://github.com/unrealbg/BlazorShop/actions/runs/40956916574).

| Check | Result at the verified revision |
| --- | --- |
| Release restore/build, warnings as errors | Passed; zero build warnings/errors |
| Unit/integration suite, including PostgreSQL 17 regressions | 694 passed, 10 opt-in live smoke tests skipped, zero failed |
| PostgreSQL 16 archive regression | 11/11 passed |
| Playwright regression suite | 4/4 passed twice consecutively; both strict scenario verifiers passed |
| npm runtime and complete dependency-tree audits | Zero vulnerabilities in both JSON reports |
| NuGet transitive audit | 11 projects, zero vulnerable packages or audit problems |
| Container builds, Compose validation and published storefront asset checks | Passed |
| Code Analysis | Passed |

Evidence: [browser TRX files and screenshots](https://github.com/unrealbg/BlazorShop/actions/runs/40956916606/artifacts/12933279028), [npm audit JSON](https://github.com/unrealbg/BlazorShop/actions/runs/40956916606/artifacts/12932784733), [NuGet audit JSON](https://github.com/unrealbg/BlazorShop/actions/runs/40956916606/artifacts/12933688130). Browser logs confirm all four named scenarios and both mandatory verifications; downloaded audit reports confirm the counts above.

The [PR validation record](https://github.com/unrealbg/BlazorShop/pull/114) identifies the final PR head and its matching successful workflow runs. Results for an earlier revision are distinguished from verification of the final documentation commit. This closure update changes only the review document.

## Visual evidence and remaining limitations

- Captures cover 320/390/768/1440px category, simple-product and variant-product layouts. The scenario products exercise the missing-image fallback; a visual check of product details populated with real product images remains outstanding. Gallery crop, proportions and image loading must be assessed with that data before considering image presentation fully verified. This limitation does not add scope to this PR.
- The initially reviewed revision displays **“1 options”** for a single variant. Existing commit [`fe7824888e358642f3cabc19f94edd16e2fa1131`](https://github.com/unrealbg/BlazorShop/commit/fe7824888e358642f3cabc19f94edd16e2fa1131) changes this to **“1 option”** with one markup-line change. It is included in the verified revision above. No further application-code change is part of this documentation closure.
- The 320px related-product grid retains two columns from the shared foundation; labels wrap without page overflow. This differs from the single-card prototype presentation.
- Chromium is covered. Firefox/WebKit, physical devices, a full accessibility audit and live production smoke tests remain unverified. The ten opt-in live smoke tests are skipped, rather than reported as passed.
- The category endpoint's existing product order and the product page's current single image remain authoritative. Prototype-only sorting, thumbnail-gallery behavior, ratings and stock claims are not implemented.

No merge, release or deployment is included in this change.
