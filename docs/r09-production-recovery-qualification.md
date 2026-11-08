# R09 production recovery qualification

R09 remains **PARTIALLY VERIFIED**. Production release and deployment remain **NO-GO** pending a separate approval. The hosted rehearsal uses synthetic data, localhost edge traffic and reconstructed baseline images.

## Recovery evidence

The [staging rehearsal](staging-upgrade-restore-rehearsal.md) runs both reviewed five-migration baseline profiles against the selected target. The recovery qualification adds a loopback TLS edge, a maintenance write denial, database-client drain, an aggregate SQL preflight, and a checkpoint manifest. It archives the three reconstructed baseline images and Compose input, checks hashes for the DB and volume archives, removes the original image tags, and reloads the images without a rebuild. Recovery starts the verified images against the restored database and the restored uploads and key ring. It uses the recovered Compose input.

The isolated refusal cases are a baseline restart before the migration, a target startup with a deliberately invalid database login, and a restore after target writes and external synthetic events. The failed login is a startup failure before EF applies changes. For failure after a committed migration, the recovery boundary is already crossed. Keep the edge closed, stop writers, preserve upgraded state and determine the last committed migration. Restore the pre-cutover checkpoint or choose recover-forward with the owner. An image-only reversal or a narrow archive migration downgrade cannot recover the full checkpoint.

Reports retain elapsed milliseconds, hashes, image IDs, synthetic aggregate counts and assertion outcomes. Archives and temporary credentials remain in the runner's runtime directory. Timings cover a small synthetic fixture and exclude real traffic, transfer, recovery approvals, and production capacity. Use a separately authorized representative test to qualify RPO and RTO.

## Proposed production evidence collection

Obtain separate written approval for the exact host, services, role, command list and evidence destination before any connection. The first session is read-only and produces metadata only.

1. Identify the exact Compose project and containers through the approved service inventory. Read each container's image ID using a narrow formatted inspect. Read only image IDs, creation timestamps, platform, RepoDigests and the OCI source/revision labels from image metadata. Record each digest type. Verify mount names and destinations without reading contents or dumping the container environment.
2. Compare the IDs and migration catalog to prior evidence. Look up an existing protected original image export or immutable registry reference, verified backup manifest and provenance. Record archive hash, source-to-image evidence, retention and recovery access. If an original image must be exported, stop and request separate authorization for the export and its protected destination.
3. With an approved read-only database role, run [the aggregate SQL](../rehearsal/recovery-preflight.sql) against the identified database and schema. Retain output in the restricted evidence store. Share only approval status or non-sensitive aggregate conclusions. Use the documented five migration IDs as the expected baseline. Do not run the script as a substitute for identifying the database.
4. Collect existence and access attestations for the protected deployment inputs, database, uploads, key ring and rollback images. Keep connection strings, rendered Compose environments, key material, user data and dumps out of GitHub Actions. Stop on changed identity, missing required artifacts, unapproved scope or unsafe outputs.

## Aggregate SQL and stop criteria

The query starts a read-only transaction with bounded statement and lock timeouts. It checks the five- and thirteen-migration schema shapes and emits counts, not individual users, orders or values. Missing historical currency or provider fields are reported explicitly. Review authoritative currency and provider evidence separately.

| Signal | Before a production cutover |
|---|---|
| Unknown or incomplete migration history, unexpected schema shape, query error or timeout | STOP. Identify the database and migration state, then repeat with the approved read-only role. |
| Orphaned roles, unexpected administrator counts or missing legitimate admin access | STOP. Validate approved privileged accounts and the session policy. |
| Negative stock, nonzero variant parent quantity, missing catalog reference, negative or nonfinite prices/totals, invalid line quantity or overlong legacy statuses | STOP. Quantify affected records privately and obtain an approved mapping or separate remediation. |
| Missing or non-EUR legacy currency proof, legacy Paid without provider evidence, unrecognized status, unsettled transaction or ambiguous archive state | STOP. Establish approved mappings, reconcile provider state and confirm the expected archive counts. |
| Incomplete checkpoint, mismatched digests, unknown image provenance or unverified restore | STOP. Recover the protected checkpoint in a separate namespace and verify baseline binaries, migration IDs, grants, role and session behavior, uploads and keys. |

Use an owner-approved off-host checkpoint tied to the writer drain. In the restore namespace, verify the manifest before creating new volumes, restore the DB with approved roles and grants, restore files with matching owners and modes, and start the exact rollback image IDs with the recovered configuration. Verify five migration IDs, session policy, uploads and private smoke. Keep the upgraded state and a separate reconciliation ledger. A payment or SMTP delivery after the checkpoint may survive a database rollback. Confirm what was already delivered before any replay and obtain an owner decision on affected writes, sessions, RPO, RTO and the maintenance window.

See the [production runbook](production-runbook.md), [archive cutover](legacy-checkout-archive.md), and [VPS baseline evidence guide](r09-vps-version-evidence.md).
