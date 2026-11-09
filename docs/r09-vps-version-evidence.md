# Establishing the deployed VPS version (R09, read-only)

R09 is **PARTIALLY VERIFIED**: the historical upgrade/restore rehearsal passes, while the VPS-specific rehearsal is **BLOCKED** because nobody has established which BlazorShop version the VPS actually runs. This page is the next step. It only reads; it changes nothing on the VPS.

## Why a checkout SHA is not enough

`compose.production.yml` builds the images from the checkout on the host, and `.dockerignore` excludes `.git`, so the images carry no commit ID. The checkout can have moved since the images were built, or have local changes. The version must therefore be inferred from what the running containers and the database contain:

- the complete list of applied EF Core migrations (IDs, count and head);
- SHA-256 hashes of files the Dockerfiles copy verbatim from the repository: `/app/appsettings.json` in the API and Storefront images, and the nginx files in the Web image (`templates/default.conf.template`, `snippets/blazorshop-security-headers.conf`, and `conf.d/default.conf`, which older Web images copied verbatim and newer ones render at start);
- whether a file is provably absent from an image;
- image creation and container start times, compared with the checkout's `HEAD` and commit time.

## Step 1: collect evidence over SSH (owner, read-only)

For a minimal production inventory, obtain separate approval for the exact host, port, SSH account, Compose project, database role, command list and protected evidence destination. Use the collector's `--inventory-only <project> <db-name> <read-only-db-user>` mode for that scope. This mode keeps the same matcher format and omits checkout inspection, volume traversal, database size and approximate row counts.

The inventory mode inspects fixed container and image fields, the three expected named volume destinations, and the OCI revision label when it contains a hexadecimal revision. It refuses other mount layouts before file probes. It hashes only the fixed matching inputs and refuses symlinked inputs. It reads the EF history in one repeatable-read, read-only transaction with 5-second statement and 1-second lock timeouts. The PostgreSQL connection uses the existing local socket at port 5432 and an explicitly supplied read-only role, without inherited PostgreSQL environment or password files. A failed connection stops collection. It also checks that the same four containers are still running with the same start times before sealing the output. Checkpoint and off-host recovery evidence require separate, owner-approved artifact references.

The wider version-discovery procedure below has a different scope.

Prerequisites:

- a local clone that is up to date and clean, with the reviewed scripts from `master`: `git fetch origin`, then `git diff --quiet origin/master -- scripts/vps-evidence` must succeed;
- an SSH account on the VPS that can run the `docker` CLI without a password prompt (member of the `docker` group, or passwordless `sudo -n`);
- `bash` 4.4 or later on the VPS (`bash --version`), and also on the machine that runs the matcher. Both scripts check this first and stop with an explanation on older versions or another shell, before running any command. The containers need no bash: the in-container probes are POSIX `sh`, and the images already contain `sh`, `sha256sum`, `find`, `printf` and `psql`.

Nothing is copied to or installed on the VPS: the script is streamed to `bash -s`. First list the running Compose projects (exit status 2 is expected):

```bash
ssh <user>@<vps> 'bash -s' < scripts/vps-evidence/collect-version-evidence.sh
```

Then collect, passing the project name and, optionally, the deployment checkout directory (shown by the `compose_working_dir` label in the output):

```bash
ssh <user>@<vps> 'bash -s -- <compose-project> <deployment-checkout-dir>' < scripts/vps-evidence/collect-version-evidence.sh > vps-evidence.txt
```

What the script does and does not do:

- **Docker commands:** it runs only `docker ps`, `docker inspect` with fixed format fields, `docker image inspect`, and `docker exec` of `sh`/`sha256sum`/`find`/`printf`/`psql` in containers that are already running.
- **Git commands:** it runs read-only `git` commands with `GIT_OPTIONAL_LOCKS=0`, so even `git status` does not rewrite the index.
- **Never:** it never runs `docker compose`, so the Compose environment and its secrets are not rendered. It never starts, stops, pulls, builds, or writes anything.
- **Database:** PostgreSQL queries run with `default_transaction_read_only=on` and `psql -w`, so psql never prompts for a password.
- **Container choice:** for each of `postgres`, `api`, `storefront` and `web` it uses exactly one *running* service container of the project, ignoring stopped and one-off (`compose run`) containers. Zero or several matches abort with a diagnostic.
- **Missing files:** `missing` (or `absent` for a directory) means the path provably does not exist: neither it nor a dangling link is there, and its nearest existing parent is a searchable directory. A path below an unsearchable directory, a `docker exec` failure or a wrong file type aborts instead.
- **File counts:** `uploads_files` and `data_protection_key_files` count regular files without a `find | wc -l` pipeline, which plain `sh` (no `pipefail`) would let report a partial count when `find` fails. Any `find` error aborts the collection.
- **All or nothing:** the evidence is printed only after everything was collected and validated, followed by a checksum line and `evidence_complete=true`. On any failure it prints a diagnostic on stderr, nothing on stdout, and exits 1. A failed run therefore leaves an empty file, never a partial one.
- **What it prints:** commit and image IDs, timestamps, hashes, migration IDs, sizes, file counts, the PostgreSQL version and approximate per-table row counts. It prints no environment variables, connection strings, keys, file contents or row data. The row counts reveal data volume, so review the file before sharing it.

If the VPS does not use the `blazorshop` database name or user, set `BLAZORSHOP_EVIDENCE_DB_NAME` / `BLAZORSHOP_EVIDENCE_DB_USER` in the remote command. A deployment without all four services (for example one that predates the Storefront) is not supported and fails closed; record it manually.

## Step 2: map the evidence to commits (local)

```bash
bash scripts/vps-evidence/match-version-evidence.sh vps-evidence.txt
```

Run it in a full clone; shallow clones are refused. The matcher behaves as follows:

1. **Rejects bad evidence (exit 2).** Malformed, incomplete, duplicated or contradictory evidence is rejected before any matching. This covers a missing completion marker, a checksum mismatch, a duplicate key, an unknown schema, a malformed hash, a count that differs from the migration lines, duplicate migration IDs, and a head that is not the newest migration.
2. **Lists candidates per service.** For each commit it lists the `api`, `storefront` and `web` candidates.
   - `api` requires the API appsettings hash and **exactly the same set of migration IDs**. A matching head with a different count or list does not match.
   - `web` compares each nginx file against the source that **that commit's Web Dockerfile** copies to the same container path. Before `6832c5c`, `nginx.conf` was copied to `conf.d/default.conf`; later it is a template.
3. **Reports the intersection.** `candidate=` lines are commits consistent with all three services. Exit 0 means at least one candidate; exit 1 means none.

## Limits of the method

- **The result is a candidate set, not proof of a build.** Matching configuration files and migrations do not prove identical binaries. Commits that change only code between two configuration changes are indistinguishable, so a typical result is a range of adjacent commits. On today's history, evidence equal to `master` yields 10 candidates (`238a336` … `f9025f7`). The owner confirms the baseline from that set, using the image times and the checkout state.
- **Services can be built from different commits.** Per-service candidates exist then, but the intersection is empty. That is a finding to resolve with the owner, not something to guess away.
- **The migration list records the newest API that migrated the database.** If an older API image was redeployed after a newer one had migrated, the API files and the migrations disagree, and the intersection is empty.
- **History coverage.** By default the matcher scans the first-parent history of `origin/master` (merge results, 119 of 586 commits today). `--all-ancestors` also scans commits of merged branches. A deployment from an unmerged branch or a modified checkout matches nothing.
- **Line endings.** For each tracked configuration file, the matcher compares the evidence hash with the exact Git blob and, for an LF text blob, with the exact bytes produced by expanding each LF to CRLF. This covers build contexts packaged from a checkout that uses CRLF, such as a Windows checkout with `core.autocrlf=true`. The conversion preserves every other byte and an unterminated last line. A blob containing CR (including mixed endings) or NUL is eligible for an exact match only. An arbitrary mixture of line endings or any other content edit is outside this rule. The collector hashes the deployed bytes and the matcher validates the original evidence checksum.
- **The checksum detects truncation and accidental edits.** It is not a signature. Trust in the evidence rests on the owner running the reviewed script over SSH and handling the file carefully.

## Step 3: decide and record the baseline (owner)

From the candidates, the image times and the checkout state, the owner confirms one baseline commit and records the evidence. Only then:

1. add `rehearsal/baselines/vps.env` with that commit, its migration count and head, and a fixture name;
2. add a seed and assertions that match that schema (the historical `historical-5` fixture covers only the 5-migration schema);
3. run the hosted rehearsal with that profile and keep its artifact.

No guessed baseline is added in the meantime. RPO/RTO for the VPS must come from the real data volume and procedure (the evidence reports database size and file counts), not from the synthetic rehearsal durations.

## Tests

`bash scripts/vps-evidence/tests/run-tests.sh` runs in the `build-test` CI job together with ShellCheck. It uses a mock `docker` CLI and synthetic git histories and covers:

- **Containers:** running, missing, duplicate, stopped and one-off containers.
- **Files:** provably missing files and directories, failed `docker exec`, non-regular files, nested file counts, a `find` that fails after a partial listing, a volume path that is not a directory, and an unsearchable parent directory (enforced only where file permissions apply, as in CI on Linux; skipped with a note on Windows or as root).
- **Database:** PostgreSQL failures and inconsistent migration data.
- **Bash version:** bash older than 4.4 and a shell that is not bash are refused before any command runs.
- **Evidence and matching:** valid and mismatched migration sets, invalid or incomplete evidence, several candidates, one candidate, zero candidates, services from different commits, merged-branch states, shallow clones, and the read-only command set.
- **Configuration bytes:** exact LF and checkout CRLF, preservation of an unterminated last line, rejection of content edits and uncommitted mixed endings, exact matching of a mixed-ending Git blob, and the legacy nginx `conf.d/default.conf` copy path.

## Out of scope

No deployment, migration, restart, configuration change, backup or restore on the VPS is part of this step.
