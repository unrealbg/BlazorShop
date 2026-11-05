# Establishing the deployed VPS version (R09, read-only)

R09 is **PARTIALLY VERIFIED**: the historical upgrade/restore rehearsal passes, while the VPS-specific rehearsal is **BLOCKED** because nobody has established which BlazorShop version the VPS actually runs. This page is the next step. It only reads; it changes nothing on the VPS.

## Why the local or VPS checkout is not enough

`compose.production.yml` builds the images from the checkout on the host, and `.dockerignore` excludes `.git`, so the images carry no commit ID. The checkout can have moved since the images were built, or have local changes. The version must therefore be inferred from what the running containers and the database contain:

- the database migration head, which bounds the version to the commits that share that newest migration;
- SHA-256 hashes of files that the Dockerfiles copy verbatim from the repository into the images (`appsettings.json` of the API and Storefront, the Web `nginx.conf` template and `nginx-security-headers.conf`), including whether a file is absent;
- image creation and container start times, compared with the checkout's `HEAD` and commit time.

Even together this yields a set of candidate commits (typically a range of adjacent commits that share those files), not a single proven build. The owner confirms the baseline from that set.

## Step 1 — collect evidence on the VPS (owner, read-only)

Run in the deployment checkout, as a user with Docker access:

```bash
bash scripts/vps-evidence/collect-version-evidence.sh
```

Without arguments it lists the running Compose projects. Then pass the BlazorShop project name:

```bash
bash scripts/vps-evidence/collect-version-evidence.sh <compose-project-name> > vps-evidence.txt
```

The script only runs `git rev-parse/log/status`, `docker ps`, `docker inspect` with fixed format fields, `sha256sum`/`du`/`find` inside the running containers, and `psql` with `default_transaction_read_only=on`. It does not run `docker compose`, so it never renders the Compose environment, and it prints no environment variables, connection strings, keys, file contents, or row data: only commit and image IDs, timestamps, hashes, migration IDs, sizes, counts and versions. Review the file before sharing it.

If the VPS does not use the `blazorshop` database name or user, set `BLAZORSHOP_EVIDENCE_DB_NAME` / `BLAZORSHOP_EVIDENCE_DB_USER`. If it does not use `compose.production.yml` at all, stop and record how it is deployed instead.

## Step 2 — map the evidence to commits (local)

```bash
bash scripts/vps-evidence/match-version-evidence.sh vps-evidence.txt
```

It lists every `master` commit whose newest migration equals the reported head and whose copied files match every reported hash (or are absent where the VPS reports `missing`). No match means the deployment is from another branch or a modified checkout, which is itself a finding.

## Step 3 — decide and record the baseline (owner)

From the candidates, the image times and the checkout state, the owner confirms one baseline commit and records the evidence. Only then:

1. add `rehearsal/baselines/vps.env` with that commit, its migration count and head, and a fixture name;
2. add a seed and assertions that match that schema (the historical `historical-5` fixture covers only the 5-migration schema);
3. run the hosted rehearsal with that profile and keep its artifact.

No guessed baseline is added in the meantime. RPO/RTO for the VPS must come from the real data volume and procedure (the evidence reports database and upload sizes), not from the synthetic rehearsal durations.

## Out of scope

No deployment, migration, restart, configuration change, backup or restore on the VPS is part of this step.
