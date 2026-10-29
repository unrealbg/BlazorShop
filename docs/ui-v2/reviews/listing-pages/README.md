# UI v2 — Storefront listing pages

Based on `master` at `82d87411ec3063ad495dc8f4deeb8120d9b2105e`, including the shared storefront foundation and category/product integration.

## Presentation

- New Releases follows `new-releases.html`: centered introduction, supported navigation, Latest Products heading and actual returned-item count.
- Today's Deals follows the warm panel and product-list composition in `todays-deals.html`. The visible heading is **Today's Featured Picks**, with current catalog prices. The API does not supply a discount selection, so the page makes no savings claim and adds no promotional prices.
- Both pages reuse the existing ProductGrid, ProductCard, layout, button styles, focus treatment and image fallback. Listing styles are scoped in `ui-v2-storefront.css`.
- Counts use returned items, with singular/plural wording. Empty results remain a successful page. API unavailability retains HTTP 503, no-store/noindex behavior and suppressed canonical/structured data, with one visible h1 and a customer-facing retry message.
- The prototype's unsupported catalog action links to the existing home route instead. No catalog route, filter, wishlist, review or promotion feature is introduced.

## Preserved contracts

The two page `@code` blocks are unchanged. New Releases retains page 1, 24 items, Newest sorting and `CreatedAfterUtc = DateTime.UtcNow.AddDays(-7)`. Today's Deals retains page 1, 12 items and Newest sorting. Metadata descriptions and CollectionPage composers remain unchanged.

Static SSR, published-only API selection, product/category slugs, image URLs, cart selectors, variant purchase navigation and authentication behavior remain unchanged. There are no changes to public API contracts, Domain, Application, schema, migrations, payment, inventory or checkout logic.

## Regression coverage

- Six HTTP integration cases cover both routes with products, empty results and API unavailability. They verify actual item counts, canonical/CollectionPage output, cart metadata, absence of invented prices, HTTP/cache/indexing semantics and the exact upstream query parameters.
- A new mandatory Playwright scenario covers both listings at 320, 390, 768 and 1440 px: returned-item counts, unpublished-product exclusion, the release-window boundary, a real image asset, missing-image fallback, exact product links, direct cart persistence, variant selection and SKU/size persistence, keyboard focus, 44 px navigation targets and horizontal overflow.
- The four existing browser scenarios are retained. The strict TRX verifier now requires all five scenarios, each passing, in both consecutive suite runs.
- Listing fixture data exists only in the isolated E2E database. The image sample uses an actual repository WebP asset; no prototype data enters production components.

## Validation evidence

Local Release restore/build completed with warnings as errors: zero build warnings/errors. The unit/integration suite passed **700 tests**, with **10 existing environment-dependent smoke skips**. PostgreSQL 16 archive regressions passed **11/11**. Runtime/full npm and transitive NuGet audits returned **zero vulnerabilities**.

The complete local Playwright suite passed **5/5 twice consecutively**, including the four existing scenarios and the listing scenario. Matching-width screenshots were inspected against the original prototypes; the deals introduction retains the prototype's left alignment while New Releases is centered.

The PR Checks tab identifies the final validated head and contains the full CI, Code Analysis, PostgreSQL, container/Compose, published-static-asset and two-run browser results. The PR description records the exact SHA and direct run/artifact links after completion; results from earlier commits are not final-head evidence.

Browser artifacts contain `run-1/ui-v2-listings/` and `run-2/ui-v2-listings/`, with these captures at each of the four widths:

- `new-releases-{width}.png` and `todays-deals-{width}.png`: rendered SSR pages with API data.
- `prototype-new-releases-{width}.png` and `prototype-todays-deals-{width}.png`: original design references.

Compare introduction alignment, warm panel, spacing, section headings, grid layout, button wrapping, image fit and footer at matching widths. Differences in product count, product content, supported navigation and deal wording are deliberate adaptations to the real application.

## Limits

- Today's Deals remains the existing newest-product selection, not a verified discount feed; the legacy route and SEO wording are preserved.
- Both pages retain their existing first-page limits. This change adds no pagination or new filtering UI.
- Empty/503 semantics are covered through HTTP integration tests; the browser commerce scenario exercises a healthy isolated API.
- Image verification covers an actual local WebP and the missing-image state. Remote uploads, broken nonempty URLs and unusual aspect ratios remain separate cases for broader image testing.
- Screenshots support human review; no pixel-diff threshold or accessibility certification is claimed. CI browser artifacts follow the existing retention policy.
- Search and general catalog recommendations are documented separately in [search-catalog-recommendation.md](search-catalog-recommendation.md); their implementation is outside this change.
