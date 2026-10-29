# Storefront foundation implementation and validation — 29 October 2026

Implementation: [PR #110](https://github.com/unrealbg/BlazorShop/pull/110). The PR description publishes the final verified commit SHA, exact CI run links and their results. The source branch includes `master` through `ec9dd432c73ce61900bd4042259de536fefe1439`, which contains the approved dependency fixes from [PR #111](https://github.com/unrealbg/BlazorShop/pull/111).

## Scope and findings

The visual references are `prototype.html`, `index.html`, `components.html`, `styles.css`, `polish.css` and `consistency.css`. Production routes, API contracts and commerce behavior remain authoritative.

The initial CI failure was dependency-related: npm full-tree audit failed before solution compilation; NuGet advisory warnings became errors in the E2E and code-analysis builds. Browser scenarios never ran. The PostgreSQL 16 archive check passed. It was therefore insufficient evidence of either UI compilation success or UI regression.

Reproduced the exact dependency chains and fixed them only in #111:

| Dependency path | Vulnerable → patched |
| --- | --- |
| Tailwind → Sucrase → glob → minimatch → brace-expansion | 2.1.4 → 2.1.7 |
| Tailwind/PostCSS nested selectors → postcss-selector-parser | 6.1.4 → 6.1.5 |
| PostCSS → source-map-js | 1.2.1 → 1.2.2 |
| Aspire 13.4.6 → StreamJsonRpc 2.25.29 → MessagePack | 2.5.302 → 2.5.303 |
| MailKit/MimeKit 4.16.0 → System.Security.Cryptography.Pkcs | 10.0.0 → 10.0.13 |

No major upgrades, advisory suppressions, severity changes or security-gate changes. MessagePack and PKCS use explicit patched references at their owning project boundaries, inherited by downstream projects. All #111 checks passed: [CI](https://github.com/unrealbg/BlazorShop/actions/runs/40943389571), [code analysis](https://github.com/unrealbg/BlazorShop/actions/runs/40943389522). The CI includes all three container builds, PostgreSQL regressions and the three mandatory E2E scenarios twice consecutively.

## Implementation

Completed the foundation presentation: shared SSR shell/header/mobile navigation/account-menu styling/footer, homepage hero, live category panels and reusable product grid/cards. Corrected the old gradient page background and black/amber footer, restored product headings and existing navigation labels, fixed missing-image rendering, and aligned typography, spacing, colors and breakpoints with the prototype. Tablet artwork retains its native proportions. Added keyboard skip navigation, visible focus, reduced-motion styling and 44px action targets. Native details/summary controls work with static SSR; no render-mode changes.

| Production files, relative to `BlazorShop.Presentation/BlazorShop.Storefront/` | Purpose |
| --- | --- |
| `App.razor` | Load versioned v2 CSS; white browser theme |
| `Components/Layout/MainLayout.razor` | Scoped shell, focusable main target, footer composition; resolve the existing session once for header/footer |
| `Components/Layout/StorefrontHeader.razor` | Supported routes, responsive navigation and skip link |
| `Components/Layout/StorefrontAccountMenu.razor` | Account label alongside the existing session-aware menu |
| `Components/Layout/StorefrontFooter.razor` | Navy footer; anonymous Sign in/Register, authenticated My Account and role-appropriate Admin Panel links |
| `Components/Public/HeroBanner.razor` | Prototype artwork/proportions; Shop now targets the live `#latest` grid with a sticky-header scroll offset, New arrivals targets `/new-releases` |
| `Pages/Home.razor` | Products before collections, live category links, remove obsolete visual helper |
| `Components/Catalog/ProductCard.razor` | Image/title/price/actions layout, semantic heading, honest missing-image state |
| `Components/Catalog/ProductGrid.razor` | Responsive shared grid and empty state from the initial slice |
| `wwwroot/css/ui-v2-storefront.css` | Namespaced foundation styles and shell-local tokens |
| `wwwroot/ui-v2/hero-chair.svg` | Known prototype asset, served from the storefront |

Test files: `BlazorShop.E2E/{StorefrontVisualTests,StorefrontCheckoutTests}.cs` and `BlazorShop.Tests/Presentation/Storefront/{StorefrontCartFlowTests,StorefrontHeaderMarkupTests,StorefrontMobileMarkupTests,StorefrontRouteSeoAuditTests,StorefrontStaticAssetsTests}.cs`. Coverage includes anonymous/customer/admin footer links, a single session-resolution call per rendered shell, distinct hero destinations, published assets, responsive controls and the original commerce flows. `scripts/verify-e2e-results.ps1` requires all four named scenarios, retaining strict failure/skip/missing/unexpected-scenario detection.

No changes to data queries, API contracts, the authentication/session resolver, structured-data composers, sitemap/robots, commerce JavaScript, product variant selection, checkout logic, database schema or migrations. The layout shares the resolver's existing result through a cascading value; header and footer do not independently refresh tokens. Authentication endpoints, cookies and authorization enforcement are unchanged.

## Verification

Validation uses the branch's committed dependency graph, without temporary dependency patches. The final commit and standalone CI results are recorded in the [PR validation report](https://github.com/unrealbg/BlazorShop/pull/110). Each linked run must match that PR head. Historical runs from #111 are dependency-fix evidence only.

| Check | Result |
| --- | --- |
| Full solution restore with `-warnaserror` | Passed |
| Full solution Release build with `--no-restore -warnaserror` | Passed, zero warnings/errors |
| Unit/integration suite | Local: 691 passed, 10 opt-in live smoke tests skipped, zero failed |
| Full transitive NuGet audit | Zero findings |
| npm runtime and full-tree audits, existing severity gates | Zero findings |
| Browser suite | Four required scenarios, twice consecutively; final standalone results and TRX artifacts linked from the PR |
| Responsive checks | 320/390/768/1440px: keyboard skip/account/mobile navigation, real navigation, no page overflow, image delivery, headings and action bounds/heights |
| Commerce checks | Card add-to-cart, live badge and reload persistence, variant-card handoff, product variant selection, quantity persistence, anonymous sign-in and authenticated checkout handoff |
| SEO/error regression | Existing rendering/audit tests passed, including 404/503 protections, canonical/structured data and discovery behavior |

`.github/workflows/ci.yml` enforces warnings as errors on solution restore and Release build, explicitly audits the complete transitive NuGet tree and retains npm audit gates. It runs unit/integration tests, PostgreSQL 16/17 regressions, all four E2E scenarios twice, Compose validation and all three container builds. A local CI container smoke check requests the published hero SVG and version-query CSS assets. NuGet/npm audit JSON and browser TRX/screenshots are uploaded as CI artifacts. Code Analysis remains a separate required workflow.

## Visual evidence and intentional differences

Screenshots use the application's real catalog API against an isolated E2E PostgreSQL database with the existing catalog seed plus temporary scenario products/users. These fixtures are not production replacement data. Final captures scroll lazy images into view before capture; baseline full-page captures may leave below-fold lazy images unloaded.

| Width | Original prototype | Initial #110 | Foundation presentation |
| --- | --- | --- | --- |
| 390 | [Prototype](prototype-390.png) | [Before](before-home-390.png) | [After](after-home-390.png) |
| 768 | [Prototype](prototype-768.png) | [Before](before-home-768.png) | [After](after-home-768.png) |
| 1440 | [Prototype](prototype-1440.png) | [Before](before-home-1440.png) | [After](after-home-1440.png) |

Additional narrow-screen evidence: [320px homepage](after-home-320.png), [320px category/cards](after-category-320.png), [1440px category/cards](after-category-1440.png). The category page body itself is deliberately still the existing design.

The production page intentionally omits unsupported search/wishlist, demo ratings/reviews, invented discounts/stock claims and prototype project/admin-preview links. The existing catalog summary does not expose stock quantities; no API contract was expanded to imitate those labels. Category panels retain all eligible API categories (up to the existing limit of eight), rather than replacing them with the prototype's three static promotional panels. The homepage retains its existing six-product query. The footer retains real support/legal navigation and reflects the same authentication state as the header. Current CI artifacts include fresh screenshots; the checked-in comparisons document the foundation's visual changes.

## Remaining risks and scope

- #110 remains Draft pending final visual approval. No merge or deployment is included.
- Category/product-detail bodies, new releases/deals page bodies, cart, supported search, account/identity, admin, checkout/payment and general error-state redesign are outside the foundation's scope.
- Chromium responsive checks are complete; Firefox/WebKit, physical-device testing and a full accessibility audit are outside this checkpoint. Desktop/tablet/mobile screenshots require independent design review. Browser and OS font rendering can differ.
- The ten live smoke tests are opt-in and were skipped; the isolated integration/E2E coverage is not a production smoke test. No real payment or production-data operation was attempted.
- Published container assets are tested locally in CI; the target host's reverse proxy, domain/cookie configuration and deployment paths still require a smoke test during an explicitly approved deployment.
- The existing screenshot comparisons add approximately 6.3 MB. Fresh per-run captures remain CI artifacts; review long-term baseline storage before extending screenshot coverage.

Implementation follows the official [.NET 10 render modes](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0) and [static files](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/static-files?view=aspnetcore-10.0) guidance, retaining the application's existing static SSR model and asset-serving pipeline.
