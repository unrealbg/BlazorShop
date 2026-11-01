# Reproduce the audit observations

These are **audit-only characterization probes**, outside the application solution and release artifacts. They do not remediate the findings. A passing probe means its assertions/observations completed; an unsafe observed outcome still appears as **FAIL** in the audit matrix.

Run from the repository root on this documentation branch, whose application source is unchanged from audited commit `9abba4d642798aa1fabfe98fca72990022fb0d4a`. Alternatively, copy this directory onto a checkout of that exact baseline. Requirements: the pinned .NET 10 SDK family, Node/npm, PowerShell, local Docker, and Playwright Chromium matching the existing E2E project. Do not point Docker at a remote service. The probe host supplies its own disposable database and synthetic credentials; do not provide production environment overrides. Use only an isolated local development machine.

The project reuses existing application registrations, real ASP.NET Identity and PostgreSQL migrations, and the existing Aspire E2E fixture. Databases and records are disposable. SMTP binds to loopback and captures synthetic messages only; Stripe is disabled. The nginx probe uses the repository template with synthetic static files and a loopback-only port. It never forwards a business request. Cleanup removes only its own container. No release, DNS, production configuration or external payment/email operation is performed.

```powershell
$auditOutput = Join-Path (Get-Location) 'artifacts/rc128-reproduction'
New-Item -ItemType Directory -Force "$auditOutput/axe" | Out-Null
npm pack axe-core@4.14.0 --pack-destination $auditOutput
tar -xf "$auditOutput/axe-core-4.14.0.tgz" -C "$auditOutput/axe"
$env:BLAZORSHOP_RC_AUDIT_DIR = $auditOutput
$env:BLAZORSHOP_E2E_ARTIFACTS_DIR = Join-Path $auditOutput 'application'
$probeProject = 'docs/rc-readiness/2026-11-01/reproduction/AuditProbes.csproj'
dotnet build $probeProject -c Release -warnaserror
pwsh docs/rc-readiness/2026-11-01/reproduction/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test $probeProject -c Release --no-build --logger 'trx;LogFileName=probes.trx' --results-directory "$auditOutput/results"
pwsh docs/rc-readiness/2026-11-01/reproduction/NginxHeaders.ps1 -OutputDirectory $auditOutput
```

Do not run a build against the same project outputs while Aspire/browser tests are active: Windows can lock their DLLs. Build first, then test with `--no-build`.

## Expected observations on the audited baseline

- `SmtpProbe`: healthy registration → loopback SMTP → real Identity email confirmation → login succeeds; refresh cookie flags are checked without recording credential values. The host explicitly selects the supported Lax cookie setting for this same-site HTTPS test.
- Missing bank configuration: checkout returns 200/success with an invented fallback account value and missing beneficiary/bank details. No transfer is made and no account value is written to evidence.
- Closed SMTP: registration returns 400 and the Identity user is absent afterwards. Accepted DATA followed by a lost acknowledgement also returns 400/deletes the user, while the captured confirmation link returns 400.
- Bank order with SMTP unavailable: the order still commits, and replaying its idempotency key returns the same single order. This is evidence **against** claiming that every mail failure rolls back commerce.
- The category-policy characterization records public lookup 404 versus checkout success for a published product under an unpublished category. Changing that policy needs an owner decision.
- A direct public catalog API query with the maximum integer page number and page size 100 records HTTP 500 from offset overflow; the SSR UI's separate bound does not protect direct API callers.
- The administrative password-change flag characterization observes ordinary login remaining available; it is not an immediate-revocation policy test.
- `BrowserProbe`: 23 axe snapshots, navigation/resource timings and screenshots. It uses Release binaries in **Development Aspire hosting**, warm loopback networking and no CPU/network throttling. It is not a production benchmark. The invalid-login snapshot shares the preceding navigation's timings. Inspect the JSON violations even when the probe passes.
- `NginxHeaders.ps1`: the plain-file control has the configured server headers, while HTML/SPA fallback/CSS responses lose them when a location defines its own cache header.

The normal 40-case Chromium suite, 11-case Firefox/WebKit coordination suites and PostgreSQL security verifiers remain independent gates. These probes do not replace them. The documented CI bootstrap failure is diagnosed using the original test unchanged:

```powershell
dotnet test BlazorShop.E2E/BlazorShop.E2E.csproj -c Release --no-build --filter 'FullyQualifiedName~TabClosesWithUncertainRefresh_AnotherTabRequiresLoginWithoutPost' -- 'Playwright.BrowserName=firefox'
```

API, demo-session, cookie and refresh values are never written to the audit JSON. Browser diagnostics contain only synthetic fixture data. The axe package is downloaded into the ignored output directory; application npm manifests and lockfiles remain unchanged. Existing build targets can regenerate the tracked utility CSS; that generated change is not part of this documentation PR.
