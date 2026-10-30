# UI v2 — Admin operations

Baseline: `807a6d6ab0c888ccf4b697a2c7ae982143217374`, including customer account/authentication, cart-cookie hardening and demo session isolation.

## Existing contracts recorded before presentation changes

| Area | Preserved behavior |
| --- | --- |
| Authorization | Admin `_Imports.razor` applies the existing Admin role requirement. API authorization, customer/admin separation, JWT/session binding, credentials, refresh and logout remain unchanged. |
| Layout | All ten admin destinations, active route, public storefront, account and logout remain reachable on desktop and mobile. |
| Dashboard | Actual order totals, fulfillment counts, sales/traffic series, periods/granularity, sorting, CSV copy/export and existing order editing. No additional KPIs. |
| Products | Existing catalog/category queries and grouping; create/edit/delete gates and confirmations; image upload; separate variants and SEO tabs. Inventory remains the stock authority. |
| Categories | Existing category CRUD, delete confirmation and separate SEO editor. No hierarchy capability is invented. |
| Inventory | Existing paged filters, authoritative product/variant stock, expected-value concurrency (`ExpectedQuantity` / `ExpectedStock`) in update requests and conflict responses. No reservation or stock policy changes. |
| Orders | Existing query/grouping/filter/sort/copy/export, snapshots, details and edit dialogs. Tracking, fulfillment and admin-note requests retain their existing handlers and server rules. |
| Users | Existing search/role/lock filters, pagination, details, role selection, lock/unlock, confirmation, password-change requirement and deactivation. Existing Identity and audit services remain unchanged. |
| SEO/redirects | One component retains both routes, metadata/canonical/OG/organization fields, redirect CRUD/deactivation, validation and delete confirmation. |
| Settings | Store/Orders/Notifications saves remain unchanged. Guest checkout stays disabled. SMTP metadata and secret-configured indication remain; no secret values are displayed. System stays read-only. |
| Audit | Existing paged search/filter/detail queries and read-only metadata; no editing or deletion. |
| Feedback | Existing loading, empty, failure, retry, success, validation and busy gates are retained. |

Existing admin services retain their established clients and request/response types. The four page code-behind files and all five inline business-handler blocks are unchanged. API controllers, Domain, Application, Infrastructure, migrations, authorization imports and session/credential handlers are unchanged.

## Presentation

The admin workspace uses the prototype navy sidebar, light header and work area, compact bordered panels, blue primary actions, clear form controls and labeled mobile table rows. All ten destinations remain available through the sidebar or the existing `WorkspaceHeader` mobile menu. Account, storefront and sign-out links remain available. A skip link and visible focus styles support keyboard navigation.

`AdminDialog` is an admin-only replacement for the existing modal presentation. Its native modal dialog provides an inert background, with an explicit Tab/Shift+Tab cycle around enabled, visible controls; Escape, backdrop and close actions retain the existing visibility/close callbacks. Focus returns to the invoking control when it remains in the document. Customer dialogs and the shared workspace header are unchanged.

Existing operation feedback is also announced and displayed inside an active dialog, so stock conflicts and validation failures remain accessible while the background is inert. The underlying toast service and mutation handlers are unchanged.

`AdminProductImage` preserves existing public image paths and absolute URLs, resolves API-relative upload paths against the already configured API origin, and displays a missing-image state on empty/failed images. Upload, product and variant handlers are unchanged. The browser workflow uploads an existing product photograph and checks that the rendered preview actually decodes.

Labels and validation descriptions are connected in admin forms and the existing admin SEO field editor. Loading and request failures expose status/alert regions. The stylesheet is scoped to the admin root and uniquely named admin components.

Unsupported prototype search/theme tools, invented KPI trends, imports, bulk actions, hierarchy and new configuration controls are omitted. Existing metrics and operations take priority over illustrative prototype content.

## Regression coverage

The strict browser verifier retains the eleven existing storefront/cart/account/authentication scenarios and requires ten admin scenarios, for **21 mandatory tests in each of two consecutive runs**:

| Area | Assertions |
| --- | --- |
| Responsive workspace | Ten screens at 320/390/768/1440 px, no page/table/dialog overflow, mobile navigation, skip link, dialog Tab containment, Escape and focus restoration, linked field errors. |
| Products | Actual image upload and preview decode, create/update/delete confirmation, variant create/update/delete, product metadata. |
| Categories | Create/update/delete and cancellation, category metadata. |
| Inventory | Filters, two-page result set, product/variant authority, successful update, real stale-value HTTP 409 without overwriting concurrent stock, explicit reload/retry. |
| Dashboard/orders | Database-derived paid revenue, period/granularity, downloaded CSV, real order snapshots, details, supported tracking/fulfillment/note updates, unchanged payment/order totals, clipboard CSV. |
| Users/audit | Search, details, role add/remove, lock/unlock, email confirmation, password-change requirement, deactivation and real audit events/details/search. Audit UI remains read-only. |
| Authorization | Customer denial on all ten admin routes and server-side endpoints; demo admin API access only with its active session, denied replay without the cookie and after logout. Existing login/refresh/logout and demo isolation scenarios remain mandatory. |
| SEO/redirects | Metadata persistence, redirect create/edit/deactivate/reactivate/delete and confirmation. |
| Settings | Store/Orders/Notifications saves, guest-checkout restriction, no secret fields, read-only System tab. Original settings are restored after testing. |
| Feedback | Real empty searches, delayed request loading, failed request alert and successful retry, at all four widths. |

Component tests cover native dialog naming and inactive content, close callback idempotence, all navigation destinations/current route, and upload/public/absolute image URL handling. Browser mutations use synthetic data in the disposable test database; generated products/categories/redirects/uploads and actor audit entries are cleaned up, and shared settings are restored.

## Evidence

Baseline application screenshots were captured at `a9d21a9b7cdfec8c0c30b0d65487522942b04654` before presentation changes: [baseline CI](https://github.com/unrealbg/BlazorShop/actions/runs/41115947061), [before screenshots](https://github.com/unrealbg/BlazorShop/actions/runs/41115947061/artifacts/13005364185). This run passed all twelve then-required scenarios twice. Final exact-head results, changed files and after artifacts are recorded in [PR #120](https://github.com/unrealbg/BlazorShop/pull/120). Evidence from the baseline does not substitute for final-head validation.

Screenshots contain synthetic test data; password fields, SMTP metadata and audit request metadata are masked. Browser traces containing authorization traffic are not recorded. No production access, email delivery, real payment or deployment is exercised.

## Limits

Chromium browser coverage and the four specified widths do not establish complete assistive-technology, Firefox or Safari compatibility. Native dialogs require a browser with `HTMLDialogElement.showModal()` support. Existing order/currency presentation, supported operation choices, pagination contracts, settings limitations and dashboard API semantics are preserved. Live smoke tests requiring an external configured environment retain their existing opt-in skips; the disposable browser suite supplies independent functional coverage.
