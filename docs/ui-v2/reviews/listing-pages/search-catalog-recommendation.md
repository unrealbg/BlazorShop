# Public search and catalog recommendation

## Existing behavior

`BlazorShop.Web/Components/Search/SearchResult.razor` exposes `/search-result/{Filter}` in the WebAssembly application. It reads `sort` and `category` from the query string, defaults to NameAscending and requests page 1 with 60 products. It has no pagination controls. Search, sorting and category changes navigate within the client; product details use a modal, and cart updates use the existing cookie service.

The shared ProductService uses the public HTTP client for `product/catalog`. That controller delegates to `IPublicCatalogService.GetPublishedCatalogPageAsync`; search does not require an authenticated product endpoint. The SSR Storefront already calls `api/public/catalog/products`, which accepts the existing ProductCatalogQuery contract with search term, category, sorting and pagination.

The WebAssembly router uses AuthorizeRouteView, but the search component has no authorization attribute. Authentication/session services remain relevant to account navigation and cart continuity; they are not a reason to require sign-in for public search.

## Recommended direction

Implement future public search and catalog presentation in the **static SSR Storefront**, with a separately approved route and URL policy. Keep account, administration and existing authentication flows in the WebAssembly application. This places public product discovery beside the canonical category and product pages, allowing useful initial HTML, normal HTTP status codes and native navigation without downloading the workspace first.

| Concern | Consequence |
| --- | --- |
| Routing | Approve the public route before implementation. Use GET form/query parameters for terms, category, sort and page. Preserve existing product/category slug links. Plan compatibility for `/search-result/{Filter}` and update all callers together; do not silently break bookmarked workspace URLs. No `/catalog` route is added by this listing change. |
| SEO | Define canonical normalization for sort/filter/page combinations and an explicit indexing policy for keyword search before release. Search pages commonly need noindex to avoid unlimited keyword URLs. Preserve indexable category/product SEO, structured data and correct unavailable responses; do not canonicalize every paginated result to page 1 without a deliberate policy. |
| API | Reuse the published catalog endpoint and ProductCatalogQuery/PagedResult contracts. Basic term/category/sort/page controls need no new business endpoint. Use the API's real pagination metadata; do not treat the existing 60-item response as the complete catalog or invent stock/discount/rating filters. |
| Authentication | Public browsing stays anonymous. Preserve existing sign-in/account handoffs, safe return paths and cart cookie/session contracts across the two hosts. Do not move authentication ownership or duplicate access tokens in the SSR UI. |
| Interactivity | Native GET forms, links and full SSR navigation can implement the supported controls. Reuse existing cart JavaScript and variant-aware product-page purchase navigation. A product modal would need separately justified client behavior; it is not required to reproduce the prototype's discovery layout. No global interactive render mode is necessary. |
| Migration/testing | Test encoded terms, blank terms, invalid query values, pagination, canonical/noindex policy, published-only results, 503 behavior, mobile controls and anonymous/signed-in cart continuity. Change workspace search callers and compatibility routing only in the separately reviewed migration. |

## Alternatives and boundary

Keeping search in WebAssembly preserves its current modal and client state with less route migration, but leaves public discovery split across applications and delays useful result HTML until the client/API load. That is reasonable for private workspace tools; it is less suitable for the public storefront's primary catalog.

No search, catalog route, API, authentication or interactivity change is implemented here. The recommendation follows the existing .NET 10 static SSR model: [render modes](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0), [routing and navigation](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing?view=aspnetcore-10.0) and [static assets](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/static-files?view=aspnetcore-10.0).
