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
| Inventory | Existing paged filters, authoritative product/variant stock, version-bearing update requests and conflict responses. No reservation or stock policy changes. |
| Orders | Existing query/grouping/filter/sort/copy/export, snapshots, details and edit dialogs. Tracking, fulfillment and admin-note requests retain their existing handlers and server rules. |
| Users | Existing search/role/lock filters, pagination, details, role selection, lock/unlock, confirmation, password-change requirement and deactivation. Existing Identity and audit services remain unchanged. |
| SEO/redirects | One component retains both routes, metadata/canonical/OG/organization fields, redirect CRUD/deactivation, validation and delete confirmation. |
| Settings | Store/Orders/Notifications saves remain unchanged. Guest checkout stays disabled. SMTP metadata and secret-configured indication remain; no secret values are displayed. System stays read-only. |
| Audit | Existing paged search/filter/detail queries and read-only metadata; no editing or deletion. |
| Feedback | Existing loading, empty, failure, retry, success, validation and busy gates are retained. |

All existing admin services use the private API client and the established request/response types. The presentation change must not modify those services, the four page code-behind files or the business handlers in inline `@code` blocks. New UI focus/navigation behavior is separate from those handlers.

## Evidence

Baseline application screenshots are captured before presentation changes using synthetic accounts, catalog items and order snapshots in the isolated browser-test database. Final comparison, changed files, exact head SHA, CI runs and remaining limits are recorded in the PR description after validation. All eleven existing mandatory browser scenarios are retained; new admin scenarios extend the strict verifier.

Screenshots and logs must contain only synthetic test data. Passwords/tokens and SMTP configuration values are not review content. No production access, email delivery, payment or deployment is exercised.
