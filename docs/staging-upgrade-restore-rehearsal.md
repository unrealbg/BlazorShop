# Hosted staging upgrade and backup-restore rehearsal

This repository contains an isolated GitHub-hosted rehearsal for upgrading a representative historical BlazorShop database and restoring its backup. It is a test-only release-readiness check. It does not target, discover, or mutate any production service.

## Pinned application boundary

The rehearsal deliberately pins both application sources:

- historical application/schema baseline: `df8a5e19d12728f0228e293404d6e2ceee743db7`;
- upgrade target: `8370f817857648f475041228ea6eaf228a3b9e34`.

The historical commit is the merge of PR #97 and is the last application version before the checkout migration series began. Its migration head is `20260424183749_AdminBackOfficeMvp`. The next commit in that series introduced `20260820134400_PreserveCheckoutProductVariants`, so using the PR #108 parent as the historical baseline would not exercise the full checkout-to-archive upgrade path.

The target migration head is `20260909075202_ArchiveLegacyCheckoutOrderItems`. The target applies these seven migrations to the historical database, in order:

1. `PreserveCheckoutProductVariants`
2. `AddOrderLinePurchaseSnapshots`
3. `AddInventoryReservations`
4. `AddCheckoutIdempotency`
5. `AddPaymentReconciliation`
6. `SeparateOrderPaymentAndFulfillmentStatuses`
7. `ArchiveLegacyCheckoutOrderItems`

Changing either pinned SHA is a deliberate reviewable change to the workflow and runner script. The application checkouts are clean, detached sources; the rehearsal does not edit historical code.

## Isolation and data provenance

The workflow runs on `ubuntu-24.04` with a unique Compose project name. PostgreSQL instances, named volumes, network, application containers, credentials, JWT key, user password, and loopback ports are generated per run. Published ports bind only to `127.0.0.1`. Cleanup addresses only that run's Compose project, volumes, temporary backup, and locally built application images.

The environment uses test URLs and credentials. Stripe, email sending, demo mode, HTTPS redirection, forwarded headers, and rate limiting are disabled explicitly. It does not load repository or production secrets. `GITHUB_TOKEN` has only `contents: read`, and checkout does not persist credentials.

The historical API creates the ordinary test user through the real registration surface, with confirmation requirements disabled in the isolated configuration. The remaining deterministic fixture is inserted directly into the already migrated historical database by [`seed-baseline.sql`](../rehearsal/seed-baseline.sql). There are no public reset endpoints, authentication bypasses, external email calls, or payment calls.

The synthetic fixture includes:

- one published category;
- one purchasable product without variants;
- one published product with a `ShoesUS` size 10 variant and distinct variant price/stock;
- two legacy orders and order lines, including case-insensitive legacy fulfillment spelling and a null historical `ShippedOn`;
- two legacy `CheckoutOrderItems` rows.

The old `OrderLines` schema has no variant identifier. The representative legacy line therefore intentionally proves the documented conservative limitation: its product name and totals can be snapshotted, but the upgrade must not invent SKU, size scale/value, color, or variant identity. Likewise, the fixture contains no old provider transaction evidence; a legacy `Status = Paid` label alone must not be converted into authoritative payment success.

## Executed procedure

The separate `Staging Upgrade and Restore Rehearsal` workflow performs the following operations:

1. Checks out the workflow tooling plus the exact baseline and target application SHAs into separate directories.
2. Builds baseline and target API, Storefront, and Web images from those unmodified sources. PostgreSQL is pinned to `postgres:16.13-alpine3.23`.
3. Starts the baseline API against an empty primary PostgreSQL volume. The baseline API applies only its own historical EF migrations.
4. Creates and verifies the synthetic fixture, starts the baseline UI, and proves catalog access, UI-independent login, and an authenticated order-history request.
5. Stops all application writers, records deterministic pre-upgrade controls, and creates a real custom-format backup with `pg_dump -Fc`. The archive must be non-empty, list the expected tables, and have a recorded SHA-256.
6. Starts the exact target API against the same primary database. The documented API-startup migration bootstrapper applies the real forward migrations before the target UI is started.
7. Checks migration history, archive rename/comment/row preservation, lifecycle mapping, immutable order-line snapshots, unchanged inventory, and the absence of invented payment, reservation, or idempotency rows.
8. Runs the attach-only Playwright scenario against that same upgraded installation and database. The test refuses to run without the exact target SHA, explicit attach mode, loopback URLs, and known upgraded-database marker.
9. Stops application writers and restores the custom backup with `pg_restore --exit-on-error` into a second clean PostgreSQL volume. This is a backup restore, not an EF migration `Down` operation.
10. Compares the restored control report byte-for-byte with the pre-upgrade report before starting any application. It then starts the exact baseline images against the restored database and verifies catalog content, login, and the protected order-history operation again.

The browser scenario covers home-to-category-to-product navigation with content assertions, a product-only cart line, exact variant selection, displayed prices, quantity change and reload persistence, anonymous checkout redirect, UI login, and both preserved cart lines at checkout start. It performs no checkout submission, payment, webhook, email, or stock mutation.

## Running and diagnosing

The supported execution environment is the GitHub-hosted workflow. Run it from **Actions → Staging Upgrade and Restore Rehearsal → Run workflow**, or change a path covered by its pull-request trigger. Local Docker availability is not treated as a prerequisite or as equivalent staging evidence.

Every run uploads a `staging-upgrade-restore-<run-id>-<attempt>` artifact for 14 days. It contains:

- exact source SHAs, runner/.NET/package/browser/PostgreSQL versions, and local image IDs;
- pre-upgrade, post-upgrade, backup metadata/content-list, and post-restore control reports;
- measured upgrade, restore, and cleanup durations;
- the mandatory browser TRX plus a failure screenshot and pre-authentication trace when that test fails;
- Docker build, application, Compose state, seed, restore, and runner logs.

The database dump, generated credentials, JWT key, authentication tokens, cookies, and authenticated browser storage state are never uploaded. The browser trace stops before credentials are entered. Logs and reports should still be reviewed before sharing beyond the repository.

A missing environment, dirty/wrong SHA checkout, startup or migration error, empty backup, failed archive validation, database mismatch, zero or skipped browser tests, restore error, restored-baseline application error, or scoped cleanup error fails the job. Failure diagnostics are captured by the exit trap; no mandatory step uses `continue-on-error`.

## Interpreting the guarantee

A green run verifies this exact historical-to-target pair, fixture, PostgreSQL 16 deployment line, and browser flow on the recorded runner/image versions. It is evidence for the documented path, not a general guarantee for arbitrary databases or future commits. It does not test production infrastructure, external integrations, live traffic draining, real payment providers, outbound email, DNS/TLS, load, or a production deployment. Operational restore still requires the normal access controls and incident procedures in the [production runbook](production-runbook.md).
