# Read-only Storefront session observation

## Problem and flow

The SSR layout and checkout inspection previously called the refresh endpoint to
discover the current account. Rendering a page therefore rotated credentials.
Concurrent page requests could reuse the same old cookie and trigger legitimate
refresh replay protection.

Storefront now reads `GET /api/internal/storefront/session`. The API looks up the
existing hashed refresh credential, checks expiry/revocation and the current
Identity sign-in eligibility, and reads the current role in the active database
context. It returns only `isAuthenticated`, `isAdmin`, `displayName`, and `email`.
It does not generate tokens, save changes, revoke a family, or modify refresh
cookies. A missing/ineligible session is represented as anonymous.

The response is presentation state. It is not an authorization credential and does
not change the principal or the JWT/role policies protecting business APIs.
Existing login, refresh, logout, checkout and payment contracts are unchanged.

## Service trust

The endpoint is disabled by default. Enabling it requires the same independent,
cryptographically random 32-byte base64 service key on API and Storefront. Provision
it through server-side secret configuration; it must never enter WebAssembly
settings, source control, URLs, browser storage or logs.

`SessionObservation:Enabled` and `SessionObservation:ServiceKey` configure this
capability. Invalid enabled configuration fails startup. The API validates the
dedicated credential header with a fixed-time comparison before demo/session
inspection. The credential authenticates only this endpoint; it never satisfies
business API authorization. Source-IP assertions, arbitrary forwarding headers,
CORS and an unlisted Swagger route are not used as proof of service identity.

HTTPS is required on both ends. Storefront checks the resolved transport URI before
attaching the key, normal certificate validation remains enabled, and redirects
cannot forward it elsewhere. The API also checks the effective secure transport.
Proxy trust and network exposure still require the separately approved deployment
review; this change does not configure them.

`SessionObservation:AllowInsecureDevelopmentTransport` exists only for explicitly
enabled, disposable Development tests. It is rejected outside Development, and
both runtime transport guards also enforce the environment boundary. Browser E2E
uses its existing isolated HTTP service connection with a newly generated key per
fixture. No production configuration or public URL is changed.

## Demo isolation

The existing demo middleware establishes the active context before observation
loads data. Refresh credentials are read from that context, so missing, mismatched
or ended demo sessions cannot fall back to normal account data. Demo markers with
no active context return anonymous or the existing middleware denial.

Observation acquires the same isolation lease in a read-only mode: it neither
touches the demo expiry deadline nor performs expired-session cleanup or cookie
deletion. Default lease behavior on existing endpoints is unchanged, including
ordinary catalog activity, refresh and explicit reset/logout. The background
cleanup service also retains its existing behavior. Storefront never forwards
observation Set-Cookie headers or overwrites a newer browser refresh cookie.

## HTTP and cache isolation

Both server-side clients disable automatic cookie containers and redirects.
Credentials are set per request, never in shared default headers. Header logging is
redacted. Observation has a two-second timeout and no automatic retry pipeline.
Catalog reads retain their resilience pipeline with unsafe-method retries disabled.

The ServiceDefaults helper has an opt-out flag so Storefront can configure these
two clients explicitly without an experimental handler-removal API. Its default
remains unchanged for API and other callers.

SSR responses carrying a refresh or demo cookie receive
`Cache-Control: private, no-store, no-cache, max-age=0`, including failures and
redirects. This privacy rule does not set `noindex` on public SEO pages. Existing
cart/checkout noindex rules, canonical URLs and discovery endpoints remain intact.

## Complete response deadline

The two-second observation deadline covers both response headers and body, so a
stalled JSON body also degrades safely without keeping SSR rendering waiting.

## Regression evidence and limits

PostgreSQL HTTP tests exercise real Identity, hashed token storage, the service gate,
demo sessions and SSR rendering. They compare token/user snapshots before and after
repeated and parallel page requests, check stale rotated credentials without family
revocation, invalid/expired/revoked credentials, confirmation, admin lock/deactivate,
user isolation, checkout destinations and the token-free response schema.

Transport tests cover denied HTTP and forged forwarding assertions, disabled
automatic cookie handling, ignored Set-Cookie, bounded failures and unsafe retries.
Existing C01 and demo JWT isolation regressions remain mandatory. Both PostgreSQL
16/17 and the unchanged 27-scenario browser suite are required by CI.

This change does not provide unified routing, atomic refresh rotation, browser
refresh coordination, trusted-proxy changes, C03 rate-limit attribution, or a
production deployment. Separate-origin cookie scope limitations remain. Existing
access JWTs retain their two-hour lifetime. Server-side observation remains
anonymous until its capability and secure transport are explicitly configured.
