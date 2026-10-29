# Storefront foundation review checkpoint — 29 October 2026

Integration: [PR #110](https://github.com/unrealbg/BlazorShop/pull/110). Separate security prerequisite: [PR #111](https://github.com/unrealbg/BlazorShop/pull/111).

## Scope and findings

Read the complete [coding-agent handoff](https://github.com/unrealbg/BlazorShop/pull/110#issuecomment-6506648906) before editing. Reviewed the eight-file initial diff at `3ebbc77`, the live SSR and WebAssembly handoffs, `prototype.html`, `index.html`, `components.html`, and all three prototype stylesheets.

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

## Implementation checkpoint

Completed the foundation presentation: shared SSR shell/header/mobile navigation/account-menu styling/footer, homepage hero, live category panels and reusable product grid/cards. Corrected the old gradient page background and black/amber footer, restored product headings and existing navigation labels, fixed missing-image rendering, and aligned typography, spacing, colors and breakpoints with the prototype. Tablet artwork retains its native proportions. Added keyboard skip navigation, visible focus, reduced-motion styling and 44px action targets. Native details/summary controls work with static SSR; no render-mode changes.

| Production files, relative to `BlazorShop.Presentation/BlazorShop.Storefront/` | Purpose |
| --- | --- |
| `App.razor` | Load versioned v2 CSS; white browser theme |
| `Components/Layout/MainLayout.razor` | Scoped shell, focusable main target, footer composition |
| `Components/Layout/StorefrontHeader.razor` | Supported routes, responsive navigation and skip link |
| `Components/Layout/StorefrontAccountMenu.razor` | Account label alongside the existing session-aware menu |
| `Components/Layout/StorefrontFooter.razor` | New reusable navy footer with working shop/support/legal/auth links |
| `Components/Public/HeroBanner.razor` | Prototype artwork/proportions and supported commerce copy/actions |
| `Pages/Home.razor` | Products before collections, live category links, remove obsolete visual helper |
| `Components/Catalog/ProductCard.razor` | Image/title/price/actions layout, semantic heading, honest missing-image state |
| `Components/Catalog/ProductGrid.razor` | Responsive shared grid and empty state from the initial slice |
| `wwwroot/css/ui-v2-storefront.css` | Namespaced foundation styles and shell-local tokens |
| `wwwroot/ui-v2/hero-chair.svg` | Known prototype asset, served from the storefront |

Test files: `BlazorShop.E2E/StorefrontVisualTests.cs` and `BlazorShop.Tests/Presentation/Storefront/{StorefrontHeaderMarkupTests,StorefrontMobileMarkupTests,StorefrontRouteSeoAuditTests,StorefrontStaticAssetsTests}.cs`. Existing behavior assertions remain; assertions tied to old visual classes were updated. Added static-asset status/content-type checks and responsive browser behavior assertions. `scripts/verify-e2e-results.ps1` now requires all four named scenarios, retaining strict failure/skip/missing/unexpected-scenario detection.

No changes to data queries, API contracts, authentication/session resolution, structured-data composers, sitemap/robots, commerce JavaScript, product variant selection, checkout logic, database schema or migrations. The dependency files from #111 were applied temporarily for local verification and excluded from #110.

## Verification

These are local integration results with #111's dependency patch applied to #110. They do **not** assert a green standalone #110 CI run.

| Check | Result |
| --- | --- |
| Full solution restore with `-warnaserror` | Passed |
| Full solution Release build with `--no-restore -warnaserror` | Passed, zero warnings/errors |
| Unit/integration suite | 688 passed, 10 opt-in live smoke tests skipped, zero failed |
| Full transitive NuGet audit | Zero findings |
| npm runtime and full-tree audits, existing severity gates | Zero findings |
| Browser suite | 4/4 passed in each of two consecutive runs: original three mandatory flows plus the responsive scenario |
| Responsive checks | 320/390/768/1440px: keyboard skip/account/mobile navigation, real navigation, no page overflow, image delivery, headings and action bounds/heights |
| Commerce checks | Card add-to-cart, live badge and reload persistence, variant-card handoff, product variant selection, quantity persistence, anonymous sign-in and authenticated checkout handoff |
| SEO/error regression | Existing rendering/audit tests passed, including 404/503 protections, canonical/structured data and discovery behavior |

Local evidence is in `artifacts/ui-after/`: `ui-tests.trx`, `test-results/run-{1,2}/e2e-run-{1,2}.trx`, npm/NuGet audit JSON and per-run screenshots. The existing mandatory-scenario verifier checks both E2E reports. Initial local attempts hit disk/memory pressure; build work was moved to E:, build servers stopped, and browser child processes used workstation GC with a heap limit. No application configuration was committed for those host constraints.

## Visual evidence and intentional differences

Screenshots use the application's real catalog API against an isolated E2E PostgreSQL database with the existing catalog seed plus temporary scenario products/users. These fixtures are not production replacement data. Final captures scroll lazy images into view before capture; baseline full-page captures may leave below-fold lazy images unloaded.

| Width | Original prototype | Initial #110 | Polished checkpoint |
| --- | --- | --- | --- |
| 390 | [Prototype](prototype-390.png) | [Before](before-home-390.png) | [After](after-home-390.png) |
| 768 | [Prototype](prototype-768.png) | [Before](before-home-768.png) | [After](after-home-768.png) |
| 1440 | [Prototype](prototype-1440.png) | [Before](before-home-1440.png) | [After](after-home-1440.png) |

Additional narrow-screen evidence: [320px homepage](after-home-320.png), [320px category/cards](after-category-320.png), [1440px category/cards](after-category-1440.png). The category page body itself is deliberately still the existing design.

The production page intentionally omits unsupported search/wishlist, demo ratings/reviews, invented discounts/stock claims and prototype project/admin-preview links. The existing catalog summary does not expose stock quantities; no API contract was expanded to imitate those labels. Both hero actions lead to the supported new-releases catalog. Category panels retain all eligible API categories (up to the existing limit of eight), rather than replacing them with the prototype's three static promotional panels. The homepage retains its existing six-product query. The footer retains real support/legal/auth navigation.

## Review boundary and remaining work

- #111 is green and ready for independent review. #110 remains draft, with its security prerequisite separate. After #111 is approved and incorporated by an authorized maintainer, update #110 from master and rerun its entire CI. No merge or deployment was performed.
- This checkpoint completes the **foundation slice**, not every storefront screen. Category/product-detail bodies, new releases/deals page bodies, cart, supported search, account/identity, admin, checkout/payment and general error-state redesign remain for the handoff's follow-up PR sequence after approval.
- Chromium responsive checks are complete; Firefox/WebKit, physical-device testing and a full accessibility audit are outside this checkpoint. Desktop/tablet/mobile screenshots require independent design review. Browser and OS font rendering can differ.
- The ten live smoke tests are opt-in and were skipped; the isolated integration/E2E coverage is not a production smoke test. No real payment or production-data operation was attempted.

Implementation follows the official [.NET 10 render modes](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0) and [static files](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/static-files?view=aspnetcore-10.0) guidance, retaining the application's existing static SSR model and asset-serving pipeline.
