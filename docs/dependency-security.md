# Dependency security verification

Issue #106 adds an explicit npm audit gate for the Web build toolchain. NuGet, npm, and container findings are separate evidence: a clean result from one ecosystem does not imply a clean result from another.

## September 2026 npm baseline and correction

The committed pre-change lockfile was installed locally on 9 September 2026 with Node `22.7.0`, npm `10.8.2`, and `npm ci --include=dev`. Effective npm `omit` configuration was empty. The full-tree audit reported three affected-package findings (two high, one low), representing four distinct advisories. The runtime-only audit reported zero findings.

| Affected package | Dependency path | Severity and advisory | Affected range / compatible fix | Applicability |
| --- | --- | --- | --- | --- |
| `browserslist@4.25.2` | `autoprefixer@10.4.21 -> browserslist` | High: [GHSA-c83g-rgw3-j3cx](https://github.com/advisories/GHSA-c83g-rgw3-j3cx); high: [GHSA-73wf-gq98-2v4g](https://github.com/advisories/GHSA-73wf-gq98-2v4g) | `<=4.28.6`; updated to `4.28.9` | Development/build dependency. The vulnerable query/custom-stat behavior is not shipped as browser or server runtime code, but it is relevant to CI/build availability. |
| `nanoid@3.3.16` | `postcss@8.5.25 -> nanoid` | High: [GHSA-2v37-7h3g-55p8](https://github.com/advisories/GHSA-2v37-7h3g-55p8) | `<3.3.18`; updated to `3.3.18` | Development/build dependency used by PostCSS, not an application runtime dependency. |
| `postcss-selector-parser@6.1.2` | `tailwindcss@3.4.17 -> postcss-selector-parser` and `tailwindcss -> postcss-nested@6.2.0 -> postcss-selector-parser` | Low: [GHSA-w9m9-85wc-3x92](https://github.com/advisories/GHSA-w9m9-85wc-3x92) | `>=6.1.0 <6.1.3`; updated to `6.1.4` | Development/build dependency parsing project-controlled Tailwind selectors. It is not shipped as browser or server runtime code. |

Only the affected transitive lockfile entries and their compatible metadata dependencies were updated. Direct dependency ranges remain unchanged, Tailwind remains on major version 3, and no forced audit fix was used.

At that verification, post-update `npm ci --include=dev`, `npm audit --include=dev --json`, and `npm audit --omit=dev --json` reported zero findings. There were no audit exceptions. npm still printed Browserslist's age warning even though the installed `browserslist@4.28.9` and `caniuse-lite@1.0.30001810` were the latest versions available from the configured registry at verification time; this was not hidden or represented as an advisory.

## October 2026 Sucrase remediation

Baseline: `master` at `ab6070daeb3ac35fb00aa7309e36f34862d052d6`, after the admin UI integration. A clean development-inclusive install reproduced three moderate affected-package findings from two advisories. The runtime audit remained clean.

```text
Before: tailwindcss 3.4.17 -> sucrase 3.35.0 -> glob 10.5.0 -> minimatch 9.0.9
After:  tailwindcss 3.4.17 -> sucrase 3.35.1 -> tinyglobby 0.2.17
                                                     -> fdir 6.5.0
                                                     -> picomatch 4.0.7
```

| Finding | Official fixed version | Selected correction |
| --- | --- | --- |
| `glob`: [GHSA-3g3m-2fjr-6pw4](https://github.com/advisories/GHSA-3g3m-2fjr-6pw4), cancellation can leave traversal work running | `glob 14.0.2`; affected range `>=9.0.0 <=14.0.1` | Remove this dependency path through the supported Sucrase patch. |
| `minimatch`: [GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249), pattern preprocessing can amplify resource use | `minimatch 11.0.1`; affected range `<11.0.1` | Remove this dependency path through the supported Sucrase patch. |
| `sucrase 3.35.0` | Inherits the affected `glob` dependency | Update to `3.35.1`, within Tailwind's existing `^3.35.0` range. |

The [Sucrase 3.35.1 changelog](https://github.com/alangpierce/sucrase/blob/fa5b7abfb5588afc20c6c28f23a95b9e347215d7/CHANGELOG.md) documents the maintainer's switch from `glob` to `tinyglobby`; [upstream PR #846](https://github.com/alangpierce/sucrase/pull/846) contains the change. Tailwind's config loader still requires Sucrase's `transform` API for TypeScript/import syntax, so Sucrase is retained. The application's scripts invoke Tailwind, not the Sucrase CLI.

The selected update changes one existing package version, adds three replacement dependency entries and removes 41 entries belonging to the old glob subtree. Every other retained lockfile entry is unchanged. `package.json`, direct dependency ranges, Tailwind 3.4.17, application source, Dockerfiles and .NET package versions are unchanged. There are no overrides, audit exceptions, forced installs or peer-dependency bypasses.

### Alternatives considered

- Updating Sucrase alone is the smallest supported path and fits Tailwind's existing semver range. Its Node engine requirement is unchanged.
- Overriding `glob` to 14.0.2 and `minimatch` to 11.0.1 would cross the original `glob ^10.3.10` / `minimatch ^9.0.4` dependency contracts. The supported parent patch makes those major overrides unnecessary.
- Tailwind's current v3 LTS also permits `sucrase ^3.35.0`, but changing the CSS generator is unnecessary for this correction. Tailwind v4 migration is outside this remediation.

### Compatibility evidence

Clean `npm ci --include=dev` and both full/runtime audit JSON reports contain zero findings after the update. Generated CSS before/after is **byte-for-byte identical**:

- 53,238 bytes; SHA-256 `f3b7ced9ef22a3cc9f9fabf7f4a332c6057e8c8b1cd9e6f32f7d64c7844e14d1`.
- 705 selector rules; selector sequence SHA-256 `ac9be8c61c0c751cb3c650f38d242dc31b88a40bfa4678e3f918cf90a2313981`.
- 11 media rules; media sequence SHA-256 `525edb9fde8f762167cd7f5e9f88a53260635e68bf1e5cf116450b824db1a65c`.

The equality covers utility classes, declarations, layout, responsive breakpoints and visibility rules. No generated stylesheet update is committed. The checked-in verification script additionally exercises Tailwind's Sucrase-backed TypeScript/ES module config loader, representative responsive visibility/grid output, the generated application stylesheet and the changed Sucrase CLI include-glob path on POSIX CI. CI uploads the stylesheet and machine-readable fingerprints, then compares the CSS in **both** published Web and Storefront images against the generated workspace asset.

On Windows, Sucrase's standalone `--project` include-glob path produced no output with both 3.35.0 and 3.35.1 in an isolated comparison. That pre-existing unused CLI limitation is recorded rather than presented as a passing Windows CLI test. The application uses the transform API, and the real Tailwind config/stylesheet checks pass on Windows. The additional CLI assertion runs on the CI platform where it is applicable. All 21 mandatory application browser scenarios remain unchanged and run twice.

Exact final commit, CI/Code Analysis runs, npm/NuGet JSON artifacts, container checks and browser evidence are recorded in the remediation pull request. An npm audit is a point-in-time dependency advisory check, not a complete application or container security assessment.

## CI policy

The primary CI job performs an explicit development-inclusive clean install and two audits before the .NET build:

- `npm audit --omit=dev --audit-level=high --json` captures the shipped runtime-only tree;
- `npm audit --include=dev --audit-level=high --json` includes development/build dependencies.

A subsequent JSON validation step requires **zero findings at every severity in both reports**. This strengthens the existing command gates: moderate/low findings cannot leave the job green, and missing, invalid or registry-error reports fail validation.

Both complete JSON reports include lower-severity findings and are uploaded as the `npm-audit-<run>-<attempt>` artifact even when an audit step fails. Nonzero audit results are not masked. Registry, npm, or artifact failures fail the job and are not interpreted as a clean audit.

Container image construction remains a separate CI check. This repository does not currently claim that successful npm or NuGet audits constitute a container vulnerability scan.
