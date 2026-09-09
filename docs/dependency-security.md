# Dependency security verification

Issue #106 adds an explicit npm audit gate for the Web build toolchain. NuGet, npm, and container findings are separate evidence: a clean result from one ecosystem does not imply a clean result from another.

## npm baseline and correction

The committed pre-change lockfile was installed locally on 9 September 2026 with Node `22.7.0`, npm `10.8.2`, and `npm ci --include=dev`. Effective npm `omit` configuration was empty. The full-tree audit reported three affected-package findings (two high, one low), representing four distinct advisories. The runtime-only audit reported zero findings.

| Affected package | Dependency path | Severity and advisory | Affected range / compatible fix | Applicability |
| --- | --- | --- | --- | --- |
| `browserslist@4.25.2` | `autoprefixer@10.4.21 -> browserslist` | High: [GHSA-c83g-rgw3-j3cx](https://github.com/advisories/GHSA-c83g-rgw3-j3cx); high: [GHSA-73wf-gq98-2v4g](https://github.com/advisories/GHSA-73wf-gq98-2v4g) | `<=4.28.6`; updated to `4.28.9` | Development/build dependency. The vulnerable query/custom-stat behavior is not shipped as browser or server runtime code, but it is relevant to CI/build availability. |
| `nanoid@3.3.16` | `postcss@8.5.25 -> nanoid` | High: [GHSA-2v37-7h3g-55p8](https://github.com/advisories/GHSA-2v37-7h3g-55p8) | `<3.3.18`; updated to `3.3.18` | Development/build dependency used by PostCSS, not an application runtime dependency. |
| `postcss-selector-parser@6.1.2` | `tailwindcss@3.4.17 -> postcss-selector-parser` and `tailwindcss -> postcss-nested@6.2.0 -> postcss-selector-parser` | Low: [GHSA-w9m9-85wc-3x92](https://github.com/advisories/GHSA-w9m9-85wc-3x92) | `>=6.1.0 <6.1.3`; updated to `6.1.4` | Development/build dependency parsing project-controlled Tailwind selectors. It is not shipped as browser or server runtime code. |

Only the affected transitive lockfile entries and their compatible metadata dependencies were updated. Direct dependency ranges remain unchanged, Tailwind remains on major version 3, and no forced audit fix was used.

Post-update `npm ci --include=dev`, `npm audit --include=dev --json`, and `npm audit --omit=dev --json` report zero findings. There are no audit exceptions. npm still prints Browserslist's age warning even though the installed `browserslist@4.28.9` and `caniuse-lite@1.0.30001810` are the latest versions available from the configured registry at verification time; this is not being hidden or represented as an advisory.

## CI policy

The primary CI job performs an explicit development-inclusive clean install and two audits before the .NET build:

- `npm audit --omit=dev --audit-level=high --json` captures the shipped runtime-only tree;
- `npm audit --include=dev --audit-level=high --json` is the full-tree high/critical gate and includes development/build dependencies.

Both complete JSON reports include lower-severity findings and are uploaded as the `npm-audit-<run>-<attempt>` artifact even when an audit step fails. Nonzero audit results are not masked. Registry, npm, or artifact failures fail the job and are not interpreted as a clean audit.

Container image construction remains a separate CI check. This repository does not currently claim that successful npm or NuGet audits constitute a container vulnerability scan.
