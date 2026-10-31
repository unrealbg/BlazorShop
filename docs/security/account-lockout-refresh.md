# Account eligibility during token refresh

## Behavior

Session renewal uses the same ASP.NET Identity sign-in eligibility rules as login:
`SignInManager.CanSignInAsync` and `UserManager.IsLockedOutAsync`. A locked or
otherwise ineligible account cannot receive a successful refresh response.
Configured account/email confirmation requirements apply to refresh as well.

An eligibility failure revokes the presented refresh token and its replacement
chain. The existing controller returns HTTP 400 with no tokens and deletes the
Secure, HttpOnly refresh cookie. The response contract is unchanged.

Successful administrative lock and deactivation revoke every unrevoked refresh
token belonging to the target account, including independent browser sessions.
Replacement links and hashed storage are preserved. Unlock and email confirmation
do not reactivate revoked tokens; the user must log in again. Role and password
changes retain their existing refresh behavior.

## Concurrent issuance and account updates

The application checks eligibility before generating tokens. Persistence checks it
again using a fresh user read while holding a PostgreSQL user-row lock inside a
short transaction. Identity account updates contend on the same row:

- If the account update commits first, the insertion is refused.
- If issuance commits first, administrative lock/deactivation subsequently revokes
  its stored refresh token before reporting success.

The persistence operation uses the existing EF execution strategy. The token's
client-generated ID identifies a previously committed insert if the commit result
was uncertain. No schema change or new distributed/session store is required.
The non-relational test provider checks eligibility without claiming PostgreSQL
concurrency guarantees.

## Regression coverage

`RefreshEligibilityHttpTests` uses the production Application/Infrastructure DI
registrations, ASP.NET Identity stores and password checks, actual authentication
and admin controllers, bearer validation, and TestServer HTTP requests against a
disposable, migrated PostgreSQL database. Only external mail/payment delivery is
disabled; Identity, token persistence and authorization are not mocked.

The suite covers administrative lock for Customer/Admin, multiple session
families, unlock, deactivation, password-failure lockout, configured confirmation
requirements, cookie deletion, role boundaries, logout, rotated-token replay, and
both orderings of concurrent token insertion/admin lock. Deterministic database
interceptor barriers coordinate the concurrency tests without timing sleeps.
Existing demo JWT/session isolation tests remain mandatory.

The test-only commit `f75b53e82dd0d2264e57b0fd1457dd990632e177` reproduces the
original behavior on the unchanged application baseline: successful login and
administrative lock are followed by an unexpected HTTP 200 refresh response.
The [pre-fix PostgreSQL 17 CI job](https://github.com/unrealbg/BlazorShop/actions/runs/41212712402/job/124570882999)
records the expected HTTP 400 versus actual HTTP 200 assertion failure. This is an
executed Identity/HTTP/database reproduction, not a mocked eligibility test.

Run the focused integration suite with Docker available:

```text
dotnet test BlazorShop.Tests/BlazorShop.Tests.csproj -c Release --filter FullyQualifiedName~RefreshEligibilityHttpTests
```

The main CI runs it on PostgreSQL 17. The PostgreSQL 16 job also includes the suite
alongside its existing archive regressions. Full CI still requires both consecutive
27-scenario Playwright executions and the existing security/build/container gates.

## Access JWT limitation and policy decisions

This change does not revoke an already issued access JWT. Such tokens remain valid
until their existing two-hour expiration, subject to normal signature, issuer,
audience, lifetime and demo-session validation. Administrative lock prevents renewal
and invalidates stored refresh sessions; it does not add a per-request account query
to bearer validation.

Immediate access revocation requires a separately approved policy and design, such
as checking an account/session version during bearer validation with an explicit
cache invalidation and availability model. A shorter access-token lifetime alone
reduces the window but does not provide immediate revocation. Global changes for
role/password updates, logout, or security-stamp validation are outside this fix.

## Framework references

- [Identity sign-in eligibility](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.signinmanager-1.cansigninasync?view=aspnetcore-10.0)
- [Identity lockout check](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.islockedoutasync?view=aspnetcore-10.0)
- [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
