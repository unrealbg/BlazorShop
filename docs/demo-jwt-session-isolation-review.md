# Demo JWT/session isolation

## Security boundary

A still-valid demo access token could satisfy ordinary API role authorization after demo mode was disabled, when the same signing material, issuer and audience remained in use. The demo middleware previously bypassed session checks in that state. This is a conditional replay of a legitimately signed token, not a signature bypass. No deployed environment or real account was used to establish the finding.

Demo bearer authentication now requires exactly one nonblank demo session claim, enabled demo mode and a matching, active, unexpired request session. An ordinary access token cannot be rebound to an active demo context. Ordinary customer/admin tokens outside that context retain their existing role authorization.

The binding runs in [`JwtBearerEvents.OnTokenValidated`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbearerevents.ontokenvalidated?view=aspnetcore-10.0), after signature, issuer, audience and lifetime validation. Those checks remain unchanged. Middleware also denies demo-marked requests when demo mode is disabled, including anonymous endpoints; parsing a marker never establishes authentication. Empty or duplicate demo claims are denied.

## Implementation

- `BlazorShop.Infrastructure/Demo/DemoTokenValidation.cs`: validates the authenticated principal against the active request session.
- `BlazorShop.Infrastructure/DependencyInjection.cs`: registers the bearer validation event.
- `BlazorShop.Presentation/BlazorShop.API/Demo/DemoSessionMiddleware.cs`: denies disabled-demo replay and invalid session markers before invoking endpoints.
- `BlazorShop.Tests/Presentation/Authentication/DemoJwtIsolationTests.cs`: HTTP regression coverage using the repository bearer configuration, token issuer and middleware.
- This review report.

The change preserves token issuance/format, expiry settings, refresh-token persistence, cookie/header names, session lifecycle, login/logout routes and API payload contracts. Domain, schema, UI, payments and inventory are unchanged. Existing expired/unknown-session responses and session recovery endpoints are preserved.

## Validation

Before the fix, 10 of the original 28 new HTTP cases failed; the disabled-demo replay reached the protected endpoint. After implementation, all 34 cases pass. Coverage includes admin/customer roles, cookie/header transport, disabled mode, missing/foreign/expired sessions, empty/duplicate claims, ordinary-token rebinding, anonymous demo requests, context restoration and invalid signatures/issuers/audiences/lifetimes. Three cases exercise the bearer event without demo middleware, verifying that it rejects demo tokens without an activated context.

Local solution restore/Release build with warnings as errors passed with zero warnings/errors. The non-container suite passed 733 tests with 10 existing live-environment smoke tests skipped. PostgreSQL tests were excluded from that local run; the full suite, containers and browser checks require the exact-head CI evidence below.

Tests generate synthetic signing material and tokens in memory and never print them. Their session manager returns controlled leases with an unopened connection; these cases verify the HTTP authentication boundary rather than database provisioning or cleanup. Existing PostgreSQL and browser suites provide the broader application regression checks.

The final PR description records the exact head SHA, CI run/artifact links, full test counts, security audits, container checks and both consecutive mandatory browser runs. CI workflows and security gates are unchanged.

## Limits

This fix adds session binding, not a general access-token revocation mechanism. Previously issued ordinary tokens retain their existing lifetime and authorization semantics. An ordinary token presented with an active demo session is now rejected; clients must use the token belonging to that demo workspace. A valid matching demo token remains subject to the existing role policy.

No production access, real payment, deployment or visual redesign is claimed. Customer account/authentication UI integration remains a separate change after security review.
