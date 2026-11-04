# Release Candidate readiness audit — 2026-11-04

## Decision

**RC NO-GO** on `master` commit **`793e87f6249c0b4848167ba573e6cfff85466fa5`** (squash merge of [#140](https://github.com/unrealbg/BlazorShop/issues/140)). Production deployment remains a separate owner decision and is not granted by this audit.

Since the [2026-11-01 audit](../2026-11-01/README.md) (`9abba4d`), the three confirmed code defects that were small and fixable (R02, R03, R14) and the two P2 hosting and accessibility defects (R04, R05) were fixed and merged, and the default branch is now protected by an active ruleset. The NO-GO is driven by what is still unproven, not by a newly found defect:

1. The current target (including the new `RegistrationConfirmationDeliveries` migration) has never been rehearsed as an upgrade/restore or installed on staging (R09, R08).
2. Production routing/trust topology, provider sandboxes and operational recovery are unverified (R08, R10).
3. Release artifacts are not bound to an approved source commit (R11, partly addressed).
4. Two intermittent E2E symptoms remain without a root cause (R01): the login-form stall has boot-readiness diagnostics, the error UI after reload is not yet instrumented. One Chromium navigation stall is also unexplained (new, N01), now with navigation diagnostics.

No P0 incident, loss of funds, production compromise or authentication bypass is known. That is not proof that none exists.

**Scope and method.** Read-only review of the repository, GitHub settings and CI evidence of 2026-11-04. No production access, deployment, migration, release dispatch, external payment or real email. CI numbers below refer to the post-merge runs on `master` listed in the evidence section.

**Snapshot.** This document records the state of `master` at `793e87f` on 2026-11-04. Later changes are not reflected; the [2026-11-01 audit](../2026-11-01/README.md) is unchanged.

**Evidence classes used below.**

- **Confirmed**: reproduced, measured, or verified in source or CI at the audited commit.
- **Hypothesis**: consistent with the evidence but not proven; the alternatives are named.
- **Not verified**: a production or operational prerequisite that has not been exercised. This is not a claim that it is broken.

## Changes since the 2026-11-01 audit

| PR | Commit | Finding | Effect |
|---|---|---|---|
| [#131](https://github.com/unrealbg/BlazorShop/issues/131) | `bd21586` | R02 | Bank Transfer fails closed on incomplete merchant configuration; no fallback account |
| [#133](https://github.com/unrealbg/BlazorShop/issues/133) | `3f4e824` | R03 / [#132](https://github.com/unrealbg/BlazorShop/issues/132) | Registration confirmation through a transactional outbox (new migration `20261101131138_AddRegistrationConfirmationOutbox`) |
| [#135](https://github.com/unrealbg/BlazorShop/issues/135) | `c359a42` | R14 | Unrepresentable catalog page windows rejected instead of HTTP 500 |
| [#138](https://github.com/unrealbg/BlazorShop/issues/138) | `623c56d` | R01 | Secondary-tab WASM boot readiness with a 30 s evidence-based budget; redacted boot diagnostics; soak workflow |
| [#136](https://github.com/unrealbg/BlazorShop/issues/136) | `238a336` | R04 | Security headers on every nginx location; CI header verifier |
| [#142](https://github.com/unrealbg/BlazorShop/issues/142) | `80b7406` | R01 | Reload readiness stage before the sign-in assertion; startup probe; diagnostics on assertion failure |
| [#141](https://github.com/unrealbg/BlazorShop/issues/141) | `b5f5672` | — | Successful `browser-e2e` artifacts kept 7 days (failures 14 days) |
| [#137](https://github.com/unrealbg/BlazorShop/issues/137) | `6010159` | R05 | Validation summary list semantics and placeholder contrast (4.31:1 → 6.86:1) |
| [#143](https://github.com/unrealbg/BlazorShop/issues/143) | `076038b` | N01 | Network diagnostics when the checkout login navigation times out |
| [#140](https://github.com/unrealbg/BlazorShop/issues/140) | `793e87f` | — | ~3.2 MiB of unused static Web assets removed (no reference; published `wwwroot` verified) |

Repository governance: ruleset **26531467** is active on `refs/heads/master` only (pull request required, squash only, linear history, strict required checks `build-test`, `postgres16-archive`, `browser-e2e`, `analyze`, no force-push or deletion, no bypass actors). `allow_update_branch` is enabled.

## Finding status

| ID | Priority | Topic | Status on `793e87f` |
|---|---|---|---|
| R01 | P1 | Firefox/WASM bootstrap intermittency | **Partly resolved.** Confirmed and fixed: secondary-tab boot readiness ([#138](https://github.com/unrealbg/BlazorShop/issues/138)) and reload readiness ([#142](https://github.com/unrealbg/BlazorShop/issues/142)) races. Open, cause unknown: 30 s login-form stall, seen once in each of soak runs [41840340704](https://github.com/unrealbg/BlazorShop/actions/runs/41840340704) and [41840345592](https://github.com/unrealbg/BlazorShop/actions/runs/41840345592); Blazor error UI after reload in `LogoutDuringCommittedRefresh` under CPU stress ([41842827032](https://github.com/unrealbg/BlazorShop/actions/runs/41842827032)), not instrumented. Issue [#134](https://github.com/unrealbg/BlazorShop/issues/134) open. |
| R02 | P1 | Bank settings fail open | **Resolved** ([#131](https://github.com/unrealbg/BlazorShop/issues/131)); missing or invalid configuration hides the method and returns HTTP 400 without commerce effects ([bank-transfer-configuration-guard.md](../../bank-transfer-configuration-guard.md)). |
| R03 | P1 | SMTP controls registration durability | **Resolved in code for registration** ([#133](https://github.com/unrealbg/BlazorShop/issues/133), CI-tested). Delivery and recovery against a real SMTP provider: not verified (R10). [#52](https://github.com/unrealbg/BlazorShop/issues/52) (templates, provider abstraction, other transactional mail) remains open. |
| R04 | P2 | nginx header inheritance | **Resolved** ([#136](https://github.com/unrealbg/BlazorShop/issues/136)), confirmed in CI against the built Web image. CSP ([#26](https://github.com/unrealbg/BlazorShop/issues/26)) is separate. |
| R05 | P2 | Accessibility defects | **Measured subset resolved** ([#137](https://github.com/unrealbg/BlazorShop/issues/137)). Not a certification; [#31](https://github.com/unrealbg/BlazorShop/issues/31) open; screen-reader and device coverage not done. |
| R06 | P2 | Hidden category vs purchase eligibility | **Open — owner decision.** Unchanged. |
| R07 | P2 | Client resilience / error observability | **Open.** [#28](https://github.com/unrealbg/BlazorShop/issues/28), [#29](https://github.com/unrealbg/BlazorShop/issues/29) unchanged. |
| R08 | P1 | Routing, trust and service transport | **Not verified — release-installation gate.** No change. |
| R09 | P1 | Current-target recovery | **Not verified, and wider than on 2026-11-01.** The hosted rehearsal is pinned to target `8370f81` (migration head `20260909075202_ArchiveLegacyCheckoutOrderItems`); `master` adds `20261101131138_AddRegistrationConfirmationOutbox`, which no rehearsal has applied, backed up or restored. |
| R10 | P1/P2 | Operational, provider and content gates | **Not verified.** No change. |
| R11 | P1 | Release provenance / governance | **Partly resolved.** Confirmed: required checks are enforced by the ruleset. Confirmed in source: `release.yml` checks out without binding `ref` to the release tag and does not compare against an approved SHA. No release or tag exists yet. |
| R12 | P2 | Accepted security-policy limits | **Open — dispositions needed.** Unchanged (see security section). |
| R13 | P2 | Performance / image pipeline | **Open.** Unchanged; see also N02 (render-blocking external resources). |
| R14 | P2 | Pagination offset overflow | **Resolved** ([#135](https://github.com/unrealbg/BlazorShop/issues/135)). |

### New observations (this audit)

- **N01 — P2, unexplained CI navigation stall.** Confirmed: on [#140](https://github.com/unrealbg/BlazorShop/issues/140)'s pre-merge head `bb618b8` ([run 41880179299](https://github.com/unrealbg/BlazorShop/actions/runs/41880179299)) page navigations stalled for about 90 s (two consecutive 30 s `load` timeouts on `/authentication/login/account/checkout`, no API traffic, the next test 29 s slower). The same build loaded the same page before and after, and the full checkout class passes with [#140](https://github.com/unrealbg/BlazorShop/issues/140)'s code, so [#140](https://github.com/unrealbg/BlazorShop/issues/140) is not the cause on the available evidence. Hypotheses, not distinguishable from that run: external CDN, local Web server or proxy, runner. Controlled E2E delays of the cdnjs stylesheet, the jsdelivr script, a local stylesheet or `blazor.webassembly.js` each reproduce the signature. [#143](https://github.com/unrealbg/BlazorShop/issues/143) now records pending requests, probes and runner load if it recurs; it did not recur in the four CI runs since that include it. Experiments: branch `codex/e2e-navigation-experiments-do-not-merge` (do not merge), [run 41884281080](https://github.com/unrealbg/BlazorShop/actions/runs/41884281080).
- **N02 — P2, render-blocking third-party resources.** Confirmed in source: `index.html` loads Font Awesome 4.7 CSS from cdnjs and Chart.js from jsdelivr synchronously in `<head>`, and the CSP allows both origins. An outage or slow response of either origin blocks the workspace boot (reproduced with E2E-only delays). Chart.js is used only by the admin dashboard. The only Font Awesome class in the Web project is in `SideNavComponent`, which no Razor markup references, and no stylesheet uses the font; whether the stylesheet is needed at all is not verified in rendering. Relates to [#25](https://github.com/unrealbg/BlazorShop/issues/25) and [#26](https://github.com/unrealbg/BlazorShop/issues/26). Not changed by this audit.
- **N03 — P3, dead stylesheet.** `wwwroot/css/icomoon.css` references `../fonts/IcoMoon-Free.ttf`, which has never existed in the repository; nothing references the stylesheet. Pre-existing; not caused by [#140](https://github.com/unrealbg/BlazorShop/issues/140).
- **N04 — P3, documentation drift.** `docs/production-runbook.md` still states that SMTP failures "bubble back to callers" in strict confirmation mode, which no longer describes registration after [#133](https://github.com/unrealbg/BlazorShop/issues/133). Its "GitHub Branch Protection" section names `main` and one required approval, while the active ruleset protects `master` with zero approvals and the four required checks above.
- **[#139](https://github.com/unrealbg/BlazorShop/issues/139)** (WASM startup probes `/api/swagger/v1/swagger.json` and gets 404 on the Web origin) is still open and visible in every browser run.

## Real release blockers

These must be closed or carry an explicit, evidenced owner disposition before an RC can be cut:

1. **R09 current-target upgrade/restore rehearsal** including the outbox migration: backup, forward migration on API startup, attach-only browser checks, restore, uploads and data-protection keys, with timings.
2. **R08 production topology proof** on the chosen hosts: edge → Web → API attribution, Storefront → API service transport, forged-header rejection, cookie scope and SameSite on the real hostnames, rate-limit buckets.
3. **R10 operational gates**: Stripe test mode end to end (if card payments are enabled), SMTP provider delivery and outbox recovery, upload persistence across container replacement, readiness monitoring, rollback timing; merchant identity, Privacy/Terms, currency and fulfillment decisions.
4. **R11 provenance**: bind `release.yml` to the tag's commit and to an approved SHA before any artifact is published; create the first release from a commit whose full CI is green.
5. **R01 / N01 stability**: a dispositioned owner decision on the remaining intermittent E2E symptoms (diagnose, or accept with explicit criteria). Required checks are green on `master` today, but the symptoms have not been explained.

## Remaining security risks

| Risk | Status | Note |
|---|---|---|
| Access JWTs remain valid up to their two-hour lifetime after lock/deactivation/logout elsewhere | Accepted limit (R12, C01) | Confirmed in source (`AppTokenManager`, 2 h). Refresh eligibility is revoked; immediate access revocation is not implemented |
| Admin "require password change" flag not enforced at login | Open (R12) | Confirmed in source: `RequirePasswordChange` is stored and set by admins but not checked in the login path. Needs owner decision |
| CSP allows `'unsafe-eval'`, `'wasm-unsafe-eval'`, cdnjs and jsdelivr scripts | Open ([#26](https://github.com/unrealbg/BlazorShop/issues/26), N02) | Confirmed: delivered only as a `<meta>` policy; the Web nginx configuration sends no CSP header. No exploit shown |
| Legacy PayPal capture surface (`PaymentController`) remains although PayPal is not offered | Open (R12) | Not exercised; decide removal or explicit disablement |
| Rate limiting by `RemoteIpAddress` behind proxies | Open (R08) | Visitors may share an edge/service bucket until the trust chain is proven |
| Dependency monitoring | Partial | npm and NuGet audits gate CI with zero findings; **Dependabot alerts and security updates are disabled** |
| Static analysis | Partial | `analyze` is a `-warnaserror` build, not CodeQL; **no code-scanning analysis is configured** |
| Secret scanning | Enabled | Push protection on; 0 open alerts; GitGuardian check passes on PRs |
| Container OS CVEs / SBOM, penetration test | Not tested (R10) | |

## Migrations

- Migration head on `master`: `20261101131138_AddRegistrationConfirmationOutbox` (creates `RegistrationConfirmationDeliveries` with a cascade FK to `AspNetUsers` and three indexes; `Down` drops the table).
- Earlier migrations since the rehearsal baseline are covered by the pinned rehearsal up to `20260909075202_ArchiveLegacyCheckoutOrderItems`, which also requires the non-rolling archive cutover in [legacy-checkout-archive.md](../../legacy-checkout-archive.md) (no old and new writers at the same time).
- Migrations run on API startup in the standard path. Before any production upgrade: verified backup, writers drained, forward migration, matching binaries, verification, and a rehearsed restore path. The outbox migration has **not** been rehearsed against a representative database.

## Staging and VPS deployment prerequisites

Owner-approved, in a disposable or staging environment first:

1. Choose and record the topology (one public HTTPS host, route ownership for SSR, `/api`, `/uploads`, `/_framework`, workspace and SPA fallback).
2. Provide all secrets from a secret store: `BLAZORSHOP_DB_PASSWORD`, `BLAZORSHOP_JWT_KEY`, base URLs (`API`, `CLIENT_APP`, `STOREFRONT`), e-mail settings when `BLAZORSHOP_EMAIL_ENABLED=true`, Stripe keys and webhook secret only if card payments are enabled, and bank merchant details if Bank Transfer is offered (R02 guard rejects incomplete settings).
3. TLS and HSTS only at the public edge; forwarded headers trusted only from the exact proxy addresses; `AllowedOrigins` reduced to served origins.
4. Persistent volumes for PostgreSQL, uploads and data-protection keys; backup before migration; tested restore.
5. Bootstrap the first administrator as an explicit deployment action.
6. Run the runbook's pre-release and deployment checklists, the SEO smoke tests against the real storefront, and login/refresh/logout/upload smoke tests.
7. Re-pin and run the hosted upgrade/restore rehearsal against the current target, and keep its artifacts.
8. Decide demo-data policy for any public demo (R12) and the R06 category purchase policy.

## Evidence

- Post-merge CI on the audited commit `793e87f`: [run 41905121373](https://github.com/unrealbg/BlazorShop/actions/runs/41905121373), all required checks green, no navigation timeout recorded.
- Post-merge CI on the other `master` commits of 2026-11-04 (`c359a42`, `623c56d`, `80b7406`, `b5f5672`, `6010159`, `076038b`) passed the full required set: Chromium 40/40 twice, Firefox 11/11, WebKit 11/11, unit/integration (1057–1062 passed, 0 failed, 10 environment-gated skips), PostgreSQL 16 archive 166/166, PostgreSQL 17 security scenarios, Release build, container images, Web security headers, npm/NuGet audits, Code Analysis.
- Exception: post-merge CI on `238a336` ([#136](https://github.com/unrealbg/BlazorShop/issues/136)) failed `browser-e2e` in Firefox ([run 41834831371](https://github.com/unrealbg/BlazorShop/actions/runs/41834831371)); root cause confirmed as the reload readiness race and fixed by [#142](https://github.com/unrealbg/BlazorShop/issues/142). Earlier, post-merge CI on `3f4e824` ([#133](https://github.com/unrealbg/BlazorShop/issues/133), 2026-11-01) also failed `browser-e2e` (R01).
- Published Web build of `793e87f` (local `dotnet publish -c Release`): none of the files removed by [#140](https://github.com/unrealbg/BlazorShop/issues/140) present; 14 local `href`/`src`/`url()` references checked, all resolved except N03; no text references to removed names.
- Issue [#134](https://github.com/unrealbg/BlazorShop/issues/134) holds the R01 evidence (timelines, negative controls); PR [#142](https://github.com/unrealbg/BlazorShop/issues/142) and PR [#143](https://github.com/unrealbg/BlazorShop/issues/143) describe their controls.
- Redacted evidence archives with SHA-256 manifests are kept by the owner outside the repository; CI artifacts expire under the repository retention policy.

## Open issues

| Issue | Relevance |
|---|---|
| [#128](https://github.com/unrealbg/BlazorShop/issues/128) | This RC audit (tracking) |
| [#134](https://github.com/unrealbg/BlazorShop/issues/134) | R01 remaining symptoms — release stability |
| [#139](https://github.com/unrealbg/BlazorShop/issues/139) | Startup swagger probe — small reliability/cleanliness fix |
| [#52](https://github.com/unrealbg/BlazorShop/issues/52) | Transactional e-mail beyond registration (templates, providers) |
| [#26](https://github.com/unrealbg/BlazorShop/issues/26) | CSP hardening — security (R12, N02) |
| [#29](https://github.com/unrealbg/BlazorShop/issues/29), [#28](https://github.com/unrealbg/BlazorShop/issues/28) | Client resilience and error handling (R07) |
| [#31](https://github.com/unrealbg/BlazorShop/issues/31) | Accessibility beyond the R05 subset |
| [#42](https://github.com/unrealbg/BlazorShop/issues/42), [#25](https://github.com/unrealbg/BlazorShop/issues/25), [#24](https://github.com/unrealbg/BlazorShop/issues/24) | Performance (R13, N02) |
| [#46](https://github.com/unrealbg/BlazorShop/issues/46), [#48](https://github.com/unrealbg/BlazorShop/issues/48), [#49](https://github.com/unrealbg/BlazorShop/issues/49), [#41](https://github.com/unrealbg/BlazorShop/issues/41), [#38](https://github.com/unrealbg/BlazorShop/issues/38), [#34](https://github.com/unrealbg/BlazorShop/issues/34), [#32](https://github.com/unrealbg/BlazorShop/issues/32), [#30](https://github.com/unrealbg/BlazorShop/issues/30), [#23](https://github.com/unrealbg/BlazorShop/issues/23) | Features or improvements; not release blockers unless the owner says so |
