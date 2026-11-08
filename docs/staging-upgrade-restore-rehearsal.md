# Hosted staging upgrade and backup-restore rehearsal

This repository contains an isolated GitHub-hosted rehearsal for upgrading a representative historical BlazorShop database, restoring its backup, and restoring the two persistent application volumes (uploads and ASP.NET Core Data Protection keys). It is a test-only release-readiness check. It does not target, discover, or mutate any production service.

## Scenarios and R09 status

**R09 remains PARTIALLY VERIFIED. RELEASE / DEPLOYMENT remains NO-GO pending production sign-off.**

| Scenario | Status | Evidence |
|---|---|---|
| **Historical upgrade/restore** (`historical` profile `df8a5e1` → `527ee48`) | **PASS** | Hosted run [42103476333](https://github.com/unrealbg/BlazorShop/actions/runs/42103476333) on PR #147 head `80f63b3`: 72/72 assertions, browser test 1/1, database, schema, uploads and Data Protection restore verified. |
| **Reconstructed candidate** (`candidate-f40b870` profile `f40b870` → `527ee48`) | **PASS** | [Hosted run 42467399535](https://github.com/unrealbg/BlazorShop/actions/runs/42467399535) recorded 148 assertions and 2 Playwright tests in each profile, and 139 PostgreSQL 16 regressions on that earlier target. |
| **VPS-specific upgrade/restore** (the version actually deployed → current target) | **BLOCKED** | The deployed baseline is unknown. It waits for read-only evidence from the VPS: see [Establishing the deployed VPS version](r09-vps-version-evidence.md). |

PR #147 was merged as `f9025f7` with a tree identical to the verified head `80f63b3`. In its post-merge `ci` run [42106323751](https://github.com/unrealbg/BlazorShop/actions/runs/42106323751), attempt 1 was cancelled when `browser-e2e` hit its 45-minute limit. That happened during the Ubuntu dependency installation for Firefox and WebKit (`playwright install --with-deps`), whose apt downloads from the runner's Ubuntu mirror stalled. Both Chromium runs had already passed 40/40. The owner approved a single rerun of that job only: attempt 2 passed with Chromium 40/40 twice and Firefox/WebKit 11/11 twice, and the install step took 26 seconds. `build-test`, `postgres16-archive` and Code Analysis were not rerun.

The historical baseline is synthetic, representative evidence for the documented upgrade path. It is **not** a claim about the version that runs on any VPS.

The results above apply to the earlier target `527ee48`. For the ASP.NET Core security remediation, use the results from the exact target commit and OCI digests recorded in its hosted run.

## Pinned application boundary

The rehearsal pins both application sources:

- baselines come from the reviewed [historical](../rehearsal/baselines/historical.env) and [candidate-f40b870](../rehearsal/baselines/candidate-f40b870.env) profiles;
- the workflow uses the full PR head SHA or, for a manual dispatch, the full commit SHA of the dispatched ref.

The workflow builds the Linux/amd64 API and Storefront OCI archives once and labels them with that SHA. Each scenario independently verifies its source checkouts and archive source labels, hashes the imported image configs, and checks that the running containers use those verified imports. The runtime probe reads `dotnet --list-runtimes` inside both final images. The scan job reads the same OCI artifacts and uploads the CycloneDX SBOMs and full vulnerability reports. The artifacts record the source SHA, OCI manifest digest, and config digest for each image.

The historical commit is the merge of PR #97 and is the last application version before the checkout migration series began. Its migration head is `20260424183749_AdminBackOfficeMvp` (5 migrations). The target has 13 migrations with head `20261101131138_AddRegistrationConfirmationOutbox`. It applies these eight migrations to the historical database, in order:

1. `PreserveCheckoutProductVariants`
2. `AddOrderLinePurchaseSnapshots`
3. `AddInventoryReservations`
4. `AddCheckoutIdempotency`
5. `AddPaymentReconciliation`
6. `SeparateOrderPaymentAndFulfillmentStatuses`
7. `ArchiveLegacyCheckoutOrderItems`
8. `AddRegistrationConfirmationOutbox`

The clean application checkouts are detached. Changing a reviewed baseline or the expected migration history requires an explicit source change. Historical code, migrations, and the baseline seed remain intact.

### Baseline profiles and the blocked VPS scenario

A baseline profile is a reviewed file in `rehearsal/baselines/` with `BASELINE_SHA`, `BASELINE_MIGRATION_COUNT`, `BASELINE_MIGRATION_HEAD` and `BASELINE_FIXTURE`. The hosted matrix selects `historical` and [`candidate-f40b870`](../rehearsal/baselines/candidate-f40b870.env) independently. The runner validates the profile, checkout and full migration list. The two baselines have identical EF migration files and use the same `historical-5` seed.

`f40b870` and `91f6fa2` share the same Git tree. The diff from `94fa210` to `f40b870` changes only Storefront and Web presentation files and the README. The API Dockerfile, `.dockerignore`, and all API Dockerfile COPY directories (API, Infrastructure, Application, Domain, ServiceDefaults) are identical. There is one reconstructed application scenario for those API/UI inputs. This identifies equivalent tracked API build inputs at the two commits. It leaves the actual production build context and binary provenance open.

To enable the VPS-specific scenario later:

1. Collect read-only evidence on the VPS: the checked-out commit and working-tree state, the running image creation times, SHA-256 of files copied verbatim into the images, the `__EFMigrationsHistory` head and count, the PostgreSQL version and volume sizes. A local checkout SHA alone is not evidence of the running containers.
2. Map that evidence to an exact commit or a narrow commit range and have the owner confirm it.
3. Add `rehearsal/baselines/vps.env` and a fixture whose seed and assertions match that schema, then run the profile in the hosted workflow. Do not add a guessed baseline.

## Isolation and data provenance

The workflow runs on GitHub-hosted `ubuntu-24.04`. Each matrix job has its own runner, Compose project name, database instances, volumes, network and generated credentials. Published ports bind only to `127.0.0.1`. The volume and key-ring helpers have no network. Cleanup addresses that job's Compose project, volumes, runtime directory and locally built application images.

The environment uses synthetic URLs and credentials. Stripe, demo mode, HTTPS redirection, forwarded headers and rate limiting are disabled. Confirmation mail is enabled for a final target probe and sent exclusively to the project-local Mailpit sink. That phase's API logs and all message bodies stay out of artifacts. The hosted workflow uses a read-only repository token and ephemeral PostgreSQL databases.

The baseline API creates two accounts through real registration, with confirmation requirements disabled for initial seeding. The candidate must give the first account Admin and the second User. The historical control must give both accounts User. Subsequent checks verify actual login, admin authorization, unchanged Identity data, rotation of a pre-upgrade refresh cookie on the target, replay and logout rejection, and acceptance of the original cookie after restore. The SQL seed supplies the deterministic catalog and legacy orders. [Baseline seed](../rehearsal/seed-baseline.sql)

The synthetic fixture includes:

- one published category;
- one purchasable product without variants;
- one published product with a `ShoesUS` size 10 variant and distinct variant price/stock;
- two legacy orders and order lines, including case-insensitive legacy fulfillment spelling and a null historical `ShippedOn`;
- two legacy `CheckoutOrderItems` rows;
- two synthetic upload files (a 1×1 PNG and 64 KiB of random bytes) written into the uploads volume;
- one random value protected by a Data Protection key ring stored in the API's key volume.

The old `OrderLines` schema has no variant identifier. The representative legacy line therefore intentionally proves the documented conservative limitation: its product name and totals can be snapshotted, but the upgrade must not invent SKU, size scale/value, color, or variant identity. Likewise, the fixture contains no old provider transaction evidence; a legacy `Status = Paid` label alone must not be converted into authoritative payment success.

## Executed procedure

The separate `Staging Upgrade and Restore Rehearsal` workflow performs the following operations:

1. Resolves the baseline profile, then checks out the workflow tooling plus the exact baseline and target application SHAs into separate directories.
2. Builds baseline and target API, Storefront, and Web images from those unmodified sources, and the rehearsal-only Data Protection probe. PostgreSQL is pinned to `postgres:16.13-alpine3.23`.
3. Starts the baseline API against an empty primary PostgreSQL volume. The baseline API applies only its own historical EF migrations. The restore volumes are asserted empty.
4. Creates and verifies the synthetic fixture, starts the baseline UI, and proves catalog access, UI-independent login, and an authenticated order-history request.
5. Writes the two upload fixtures, reads them back through the API's `/uploads` path, and protects a random value with the probe against the API's key volume.
6. Records deterministic pre-upgrade controls (including exact row counts for every table), stops all application writers, and records a schema-only dump.
7. Creates a custom-format database backup with `pg_dump -Fc`. The archive must be non-empty, list the expected tables, and have a recorded SHA-256.
8. Records SHA-256 manifests of both volumes, archives each volume, tests each archive with `gzip --test`, and checks that it lists every file.
9. Starts the exact target API against the same primary database. The documented API-startup migration bootstrapper applies the real forward migrations before the target UI is started.
10. Checks migration history, every table's row count against the pre-upgrade counts (only the archive rename is mapped; tables added by the upgrade must be empty), archive rename/comment/row preservation, lifecycle mapping, immutable order-line snapshots, unchanged inventory, the absence of invented payment, reservation, idempotency, or outbox rows, and the `RegistrationConfirmationDeliveries` columns, indexes, check constraints and `ON DELETE CASCADE` foreign key to `AspNetUsers`. The cascade is also exercised in a transaction that is always rolled back.
11. Checks that the upgrade left the uploads volume unchanged, kept every pre-upgrade key, still serves the uploads, and can still unprotect the value.
12. Runs the attach-only Playwright scenario against that same upgraded installation and database. The test refuses to run without the exact target SHA, explicit attach mode, loopback URLs, and known upgraded-database marker.
13. Stops application writers and restores the database backup with `pg_restore --exit-on-error` into a second clean PostgreSQL volume. This is a backup restore, not an EF migration `Down` operation. The restored control report and schema dump must match the pre-upgrade ones byte for byte.
14. Proves with a negative control that the still-empty restore key volume cannot unprotect the value and gains no key from the attempt. It then restores both volume archives into new, empty restore volumes, compares their manifests byte for byte with the pre-backup manifests, and unprotects the value with the restored key ring.
15. Starts the exact baseline images against the restored database and restored volumes, and verifies catalog content, login, the protected order-history operation, and that both uploads are served unchanged.

The browser scenario covers home-to-category-to-product navigation with content assertions, a product-only cart line, exact variant selection, displayed prices, quantity change and reload persistence, anonymous checkout redirect, UI login, and both preserved cart lines at checkout start. It performs no checkout submission, payment, webhook, email, or stock mutation. Its selectors match the current storefront and checkout UI, which the green `StorefrontCheckoutTests` in `BlazorShop.E2E` exercise with the same texts and roles.

Before upgrade, a second Playwright scenario selects a Storefront variant, signs in through Web and opens the protected order history. On the upgraded database, the HTTP checks submit offline Cash on Delivery checkout through Web, verify the variant snapshot, retry with the same key and a conflicting intent, and race two requests for the last unit. The final Mailpit check waits for outbox delivery, verifies a single captured email and exercises the actual Identity confirmation endpoint with invalid and valid tokens. A separate job builds and runs the exact target's PostgreSQL regression suites, including fake-provider reconciliation and SMTP failure/retry tests, on disposable PostgreSQL 16.13. These regressions use their own database.

The Data Protection probe uses its own application name and purpose and verifies the backed-up key ring with positive and empty-ring negative controls. In the pinned applications, the exercised refresh cookie carries an opaque database-backed credential, and email confirmation uses Identity's email token provider. Their positive and negative API checks exercise those mechanisms independently of the Data Protection probe.

## Running and diagnosing

The supported execution environment is the GitHub-hosted workflow. Run it from **Actions → Staging Upgrade and Restore Rehearsal → Run workflow**, or change a path covered by its pull-request trigger. Local Docker availability is not treated as a prerequisite or as equivalent staging evidence.

Each matrix job uploads a `staging-upgrade-restore-<profile>-<run-id>-<attempt>` artifact for 14 days. It contains:

- exact source SHAs, runner/.NET/package/browser/PostgreSQL versions, and local image IDs;
- `result.txt` with the baseline profile and SHAs, migration heads before and after the upgrade, backup, upgrade and restore durations for the database and the volumes, the uploads, Data Protection, volume-restore and browser results, and the first failing command when a run fails;
- `assertions.txt` with every PASS/FAIL assertion and, for a failure, the expected and received values;
- pre-upgrade, post-upgrade, backup metadata/content-list, and post-restore controls, per-table counts and restore fingerprints, and pre-upgrade and post-restore schema dumps;
- volume archive checksums, sizes and file counts, the upload fixture checksums, and the uploads manifest;
- the mandatory browser TRX plus a failure screenshot and pre-authentication trace when that test fails;
- Docker build, application, Compose state, seed, restore, and runner logs.

The database dump, volume archives, Data Protection keys and key manifest, the protected probe value, generated credentials, JWT key, authentication tokens, cookies, and authenticated browser storage state are never uploaded; they stay in the run's temporary directory, which is deleted on exit. The browser trace stops before credentials are entered. Logs and reports should still be reviewed before sharing beyond the repository.

A missing environment or profile, dirty/wrong SHA checkout, startup or migration error, empty backup, failed archive validation, database or schema mismatch, changed or invented rows, missing or changed upload or key, failed probe, zero or skipped browser tests, restore error, restored-baseline application error, or scoped cleanup error fails the job. Failure diagnostics are captured by the exit trap; no mandatory step uses `continue-on-error`.

## Interpreting the guarantee

A green run verifies this exact historical-to-target pair, fixture, PostgreSQL 16 deployment line, persistent-volume layout, and browser flow on the recorded runner/image versions. It is evidence for the documented path, not a general guarantee for arbitrary databases, the deployed VPS version, or future commits. The recorded durations come from a tiny synthetic dataset on a hosted runner; they are not an RPO or RTO for any real installation. It does not test production infrastructure, external integrations, live traffic draining, real payment providers, outbound email, DNS/TLS, load, or a production deployment. Operational restore still requires the normal access controls and incident procedures in the [production runbook](production-runbook.md).
