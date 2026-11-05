# R11 — Release provenance

Status: **implemented in `.github/workflows/release.yml` and `scripts/release/` (merged in #145 as `3e5bc68`); tag rulesets and release immutability active since 2026-11-05; the real tag-triggered path has not been exercised yet.** Source finding: R11 in the [2026-11-04 RC audit](../rc-readiness/2026-11-04/README.md). Baseline before this change: `master` at `3ef5024`.

## 1. Goal

Every artifact attached to a GitHub release is built from exactly the commit the release tag points to. That commit is in `master`'s history and has passed the required CI gate. Every artifact is checksummed and attested. Assets are uploaded only to a new **draft** release and are never replaced. Publishing the draft is an explicit owner decision and does not deploy anything.

## 2. What changed and why

| Before (`3ef5024`) | Now |
|---|---|
| Triggers `release: published` and `workflow_dispatch` (free-text `tag`) | Only a push of a `v*` tag; the tag must be `vMAJOR.MINOR.PATCH` or `vMAJOR.MINOR.PATCH-rc.N` |
| `workflow_dispatch` checked out the default branch but uploaded under the supplied tag | No manual path. The build checks out the tagged commit, and the guard proves `HEAD` equals the resolved commit |
| `gh release upload … --clobber` replaced existing assets | A new draft is created with all assets in one step. A run fails if any release or draft already exists for the tag |
| Only unit tests; no check of the commit's CI | The commit must be in `master`'s history with green `build-test`, `postgres16-archive`, `browser-e2e`, `analyze` from push runs on `master` |
| One job with `contents: write`; credentials persisted; npm ran in it | Three jobs with least privilege; npm and MSBuild run only in the read-only build job; no persisted credentials |
| No provenance | `SHA256SUMS`, `provenance.json` and GitHub artifact attestations, verified before every hand-off |

## 3. Release procedure

1. Choose a commit on `master` whose post-merge CI is fully green.
2. The owner creates the tag on that commit, for example `git tag -a v1.0.0-rc.1 <sha> -m "v1.0.0-rc.1"` then `git push origin v1.0.0-rc.1`. Annotated tags are recommended; lightweight tags are accepted.
3. `release.yml` runs automatically:
   - **validate-build** verifies the tag and source, builds and packages;
   - **attest** creates and verifies attestations;
   - **publish-draft** re-verifies everything and creates a **draft** release. An `-rc.N` tag also marks it as a pre-release.
4. The owner reviews the draft: commit SHA, assets, `provenance.json`, and `SHA256SUMS` with `gh attestation verify` (see §6).
5. Only after explicit approval, and with release immutability enabled for the repository, the owner publishes the draft in the GitHub UI.

Publishing deploys nothing: no workflow is triggered by the release events.

A failed or incomplete run leaves at most a draft. Fixing it means deleting that draft manually and running again; if the commit itself is wrong, use a **new** tag. The workflow never edits, re-uploads or deletes releases or tags.

## 4. Pipeline and permissions

```
v* tag push ──► validate-build   permissions: contents read, actions read, checks read
                 1 checkout tagged commit (persist-credentials: false)
                 2 VerifySource: context, SemVer, tag → commit (annotated tags peeled),
                   event SHA, master history, 4 required checks, HEAD == commit
                 3 setup .NET (global.json), Node 24, npm ci (lockfile), dotnet publish ×3
                 4 tar.gz ×3 (sorted, fixed mtime/owner, gzip -n), provenance.json, SHA256SUMS
                 5 VerifyArtifacts, upload workflow artifact (outputs: SHA, sums hash, file list)
            ──► attest           permissions: id-token write, attestations write, contents read
                 sparse checkout of scripts/release/ only; download; VerifyArtifacts;
                 attest-build-provenance (3 archives + provenance.json); VerifyAttestations
            ──► publish-draft    permissions: contents write, attestations read
                 only for unrealbg/BlazorShop tag pushes; sparse checkout of scripts/release/;
                 download; VerifyArtifacts; VerifyAttestations; AssertTagUnchanged;
                 AssertNoRelease; gh release create --draft --verify-tag (no --clobber)
```

- **Workflow level:** `permissions: {}`. Each job declares only what it needs.
- **Concurrency:** one group per tag (`release-${{ github.ref }}`, no cancellation), so runs for the same tag never overlap. If a second run starts anyway, `AssertNoRelease` or the API's one-release-per-tag rule makes it fail.
- **Write-capable job:** it runs no npm, MSBuild, .NET SDK or project code. It runs only the release guard scripts from the verified commit, `gh`, and the official `download-artifact` action.
- **Caches:** no `setup-node` or NuGet caches are used in the release build, to avoid cache poisoning from other refs.
- **Untrusted values:** workflow expressions are passed to scripts only through `env`, never interpolated into script text.

## 5. Fail-closed conditions

Every condition below stops the run before anything is published. Rules are implemented in `scripts/release/ReleaseGuard.psm1`.

| Rule | Fails when |
|---|---|
| `context` | Event is not `push`; ref is not a tag; repository is not `unrealbg/BlazorShop`; workflow is not `.github/workflows/release.yml` at that tag |
| `tag-format` | Tag is not `^v(0\|[1-9]\d*)\.(0\|[1-9]\d*)\.(0\|[1-9]\d*)(-rc\.(0\|[1-9]\d*))?$` |
| `tag-resolution` | Tag missing; points to a non-commit; nested tag objects; annotated tag names another tag; event SHA is neither the commit nor the tag object; tag moved between build and publish |
| `master-history` | `compare/<sha>...master` is not `identical` or `ahead` with `behind_by = 0` |
| `required-checks` | Any of `build-test`, `postgres16-archive`, `browser-e2e`, `analyze` is missing, pending, failed or conflicting. Only push runs on `master` for exactly that SHA count, so pull-request results (PR heads, synthetic merge commits) are ignored. Each result must be a check run of the GitHub Actions app (id 15368, slug `github-actions`) on that SHA with conclusion `success` |
| `checkout-head` | `git rev-parse HEAD` differs from the resolved commit |
| `artifact-integrity` | `SHA256SUMS` differs from the build job's recorded hash, any file hash differs, or files are missing, extra or listed twice |
| `attestation` | `gh attestation verify` fails for any archive or `provenance.json` with `--signer-workflow unrealbg/BlazorShop/.github/workflows/release.yml --source-ref refs/tags/<tag> --source-digest <sha> --deny-self-hosted-runners` |
| `release-immutability` | Any release or draft already exists for the tag (the release list API includes drafts) |

## 6. Artifact verification

**Release assets:**

- `blazorshop-api-<tag>.tar.gz`, `blazorshop-storefront-<tag>.tar.gz`, `blazorshop-account-<tag>.tar.gz`;
- `provenance.json`: schema `blazorshop-release-provenance/v1`. It records the repository, tag, tag object SHA, commit SHA, workflow ref, run ID and attempt, runner OS, .NET SDK, Node and npm versions, the required check run IDs and their workflow runs, and each archive's name, SHA-256 and size. It also records `reproducible_build: false`;
- `SHA256SUMS`: hashes of the three archives and `provenance.json`.

**Verifying a downloaded release:**

```bash
sha256sum -c SHA256SUMS
gh attestation verify blazorshop-api-<tag>.tar.gz --repo unrealbg/BlazorShop \
  --signer-workflow unrealbg/BlazorShop/.github/workflows/release.yml \
  --source-ref refs/tags/<tag> --source-digest <commit-sha> --deny-self-hosted-runners
```

Archives use sorted entries, the commit time as mtime, numeric root ownership and `gzip -n`. **The build is not claimed to be reproducible:** NuGet restore is not locked (no `packages.lock.json`) and .NET publish output has not been compared across runs.

## 7. Tests

- `scripts/release/tests/ReleaseGuard.Tests.ps1` runs in the `build-test` CI job and offline: `pwsh ./scripts/release/tests/ReleaseGuard.Tests.ps1`.
- The API is stubbed; git and file checks use temporary directories. It never contacts GitHub or creates tags or releases.
- It covers every rule in §5 positively and negatively:
  - invalid SemVer;
  - unauthorized event, repository and workflow contexts;
  - unapproved SHA, missing, tree and nested tags;
  - SHA outside `master`;
  - failed `browser-e2e`, a missing check, PR-only or merge-SHA results, a pending result, a conflicting re-run, a non-Actions app;
  - HEAD mismatch;
  - modified archive, replaced `SHA256SUMS`, extra or missing files;
  - a failed attestation;
  - an existing release or draft.
- Static policy checks on `release.yml`:
  - tag-only trigger;
  - no `--clobber` or release mutation commands;
  - draft and `--verify-tag`;
  - exact per-job permissions;
  - no persisted credentials;
  - no build tooling in the attest and publish jobs;
  - per-tag concurrency and repository gate;
  - no override of the attested source ref.

## 8. Tag protection — applied 2026-11-05

Two rulesets, so that the bypass covers only creation. Both validate against GitHub's REST schema and were created exactly as shown below, with `master` at `3e5bc68`.

| Ruleset | ID | Enforcement | Scope | Rules | Bypass |
|---|---|---|---|---|---|
| `release-tags-immutable` | 26626203 | active | `refs/tags/v*` | `update`, `deletion` | none, including the owner |
| `release-tags-creation` | 26626214 | active | `refs/tags/v*` | `creation` | `User` 3398536 (`unrealbg`), `always` |

GitHub accepted the individual `User` bypass, so the `RepositoryRole` fallback below was not needed. The settings were verified by reading both rulesets back through the API; the `master` ruleset 26531467 was unchanged. No tag was created to test them, so enforcement against a real push has not been observed yet.

**Release immutability** was enabled for the repository on 2026-11-05 (`PUT /repos/unrealbg/BlazorShop/immutable-releases`, read back as `"enabled": true`). It applies to releases published after that date; a draft stays editable until it is published.

`release-tags-immutable`: nobody can move or delete a release tag.

```json
{
  "name": "release-tags-immutable",
  "target": "tag",
  "enforcement": "active",
  "bypass_actors": [],
  "conditions": { "ref_name": { "include": ["refs/tags/v*"], "exclude": [] } },
  "rules": [{ "type": "update" }, { "type": "deletion" }]
}
```

`release-tags-creation`: only the owner account (`unrealbg`, id 3398536) can create release tags. If GitHub rejects a `User` bypass for this personal repository, use `{ "actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "always" }` (repository admin) instead.

```json
{
  "name": "release-tags-creation",
  "target": "tag",
  "enforcement": "active",
  "bypass_actors": [{ "actor_id": 3398536, "actor_type": "User", "bypass_mode": "always" }],
  "conditions": { "ref_name": { "include": ["refs/tags/v*"], "exclude": [] } },
  "rules": [{ "type": "creation" }]
}
```

SemVer format is enforced by the workflow. GitHub's `tag_name_pattern` rule is not proposed, because its availability for this personal repository is unverified.

A wrong tag cannot be deleted while `release-tags-immutable` is active. That is deliberate: use a new version, or temporarily disable the ruleset as a recorded owner decision.

**Signed tags.** Not required for now. On 2026-11-05 the owner account had **no GPG and no SSH signing keys** registered on GitHub, and local git had no signing configuration. A `required_signatures` rule would therefore block every release tag. Prerequisite for later: register a signing key, create signed annotated tags, then add `{ "type": "required_signatures" }` to `release-tags-immutable`.

## 9. Trust boundary and residual risks

- **The workflow definition comes from the tagged commit.** A tag on a commit outside `master` runs that commit's `release.yml`, which an attacker controlling the commit could change. The guards cannot prevent that by themselves. The protection is that only the owner can create `v*` tags (§8), that `master` is protected by ruleset 26531467, and that publishing is a manual review of a draft.
- **Attestations bind the artifacts to this workflow file, the tag ref and the commit digest.** They do not prove that the build is reproducible.
- **Annotated-tag event SHA.** For annotated tags the guard accepts the event SHA if it equals either the commit or the tag object. The attestation source digest must equal the commit. If GitHub reports the tag object as the source digest, attestation verification fails closed. Confirm this on the first `-rc` tag before relying on annotated tags.

## 10. Prerequisites for the first real release

1. ~~Owner approval and application of the tag rulesets (§8).~~ Done 2026-11-05 (rulesets 26626203 and 26626214).
2. ~~Release immutability enabled in repository settings before publishing any release.~~ Done 2026-11-05.
3. A first `vX.Y.Z-rc.1` tag on a fully green `master` commit, owner review of the draft, and verification with §6. The tag-triggered path itself has not run yet; the implementing pull request's dry run documents which parts were exercised against the real GitHub API.
4. The remaining audit gates: R09, R08, R10 and R01/N01 in the 2026-11-04 audit.

Container images are out of scope: `compose.production.yml` builds images from source on the deployment host, so a deployment must check out the verified tag's commit.
