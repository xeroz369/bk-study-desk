# Security testing process

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](../quy-trinh-bao-mat.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](security-process.md)

> Translated from docs/quy-trinh-bao-mat.md (Vietnamese) for BK Study Desk 1.1.7.

How BK Study Desk is checked for security problems: which tools, when they run, and how results are handled. To report a vulnerability, see [SECURITY.md](SECURITY.md).

Principle: use existing, maintained tools (no home-made scanners), run them automatically on GitHub Actions, and read results under **Security** > **Code scanning**.

## 1. What needs protecting

| Asset | Where | Risk if leaked or changed |
|---|---|---|
| HCMUT sessions (SSO, LMS, MyBK cookies) | WebView2 profile in `%LOCALAPPDATA%\BKStudyDesk.Data` | someone signs in as the student |
| LMS token | DPAPI-encrypted file in a folder only the user's Windows account can read | LMS data read on the user's behalf |
| Study data (grades, timetable, curriculum, practice results) | `data\*.json` | personal data exposed |
| Updates and installers | GitHub Releases, the `updates` feed | malware installed on users' PCs |
| Source and CI | GitHub repository, workflows | code injected into a release |

## 2. Attack surface and controls

| Surface | Control |
|---|---|
| Practice (Avalonia controls, no web page) | no network port, no browser; results and packs go through `ApiRouter` called directly in the app; external links open in the browser |
| Messages from the Practice frame to the app (`postMessage`) | accepted only from the app's host, only shortcut and page-open commands |
| Quiz packs from others (Study Pack, Markdown, Moodle XML, GIFT, Aiken) | `validatePack` blocks scripts, event attributes and external images; pack ids use safe characters only and can't write outside the packs folder |
| File paths sent by the page | `Paths.StudyPath` only allows paths inside the study folder |
| Hidden WebView reading MyBK | only university domains over https; http within the same domain is upgraded to https; new windows are blocked |
| Actions on LMS/MyBK | read-only by behaviour: view, open forms, load lists; never submit registrations, cancellations, saves or payments |
| App updates | Velopack checks SHA-256; the only source is the GitHub repository over https; installers carry a GitHub Actions build attestation |
| CI and supply chain | actions pinned to commit SHAs; default token permissions empty, granted per job; `main` only accepts PRs that pass checks; the Store submission job needs manual approval |

## 3. Tools and when they run

| Tool | Checks | Every PR | Weekly | File |
|---|---|---|---|---|
| CodeQL | security bugs in C#, TypeScript (studypack tools) and workflows | yes | yes | `codeql.yml` |
| NuGet advisories (`dotnet list package --vulnerable --include-transitive`) | .NET packages with published vulnerabilities, including transitive ones | yes | yes | `security.yml` (deps) |
| npm audit | npm packages with vulnerabilities (moderate or higher fails) | yes | yes | `security.yml` (deps) |
| OSV-Scanner (Google) | every lockfile against OSV.dev | yes | yes | `security.yml` (osv) |
| gitleaks | committed keys, tokens and passwords across the whole history | yes | yes | `security.yml` (secrets) |
| zizmor | security problems in GitHub Actions workflows | yes | yes | `security.yml` (zizmor) |
| Semgrep (community rules) | dangerous code patterns in C#, TS/JS, secrets in code | yes | yes | `security.yml` (semgrep) |
| BinSkim (Microsoft) | built .exe/.dll files: ASLR, DEP, CFG, safe compiler flags | yes | yes | `security.yml` (binskim) |
| OpenSSF Scorecard | repository practices (branch protection, pinned actions, token permissions...) | on `main` | yes | `scorecard.yml` |
| Dependabot | PRs for vulnerable or outdated packages | | yes | `.github/dependabot.yml` |
| Personal-trace scan | names, student IDs, emails, machine paths in code and builds before exporting to the public repo | before each export | | `tools-dev/leak-scan.ps1` |

Run manually: **Actions** > **security** > **Run workflow**.

## 4. Handling results

| Severity | Examples | Deadline |
|---|---|---|
| Critical / High | token leak, code execution, writing outside the app folders, requests that change LMS/MyBK data, exploitable vulnerable package | fix and ship a patch within 7 days, not bundled with features |
| Medium | hard-to-reach vulnerabilities, loose CI configuration | fix within 30 days, in the next release |
| Low / informational | hardening advice, warnings that don't apply | backlog |

- Every alert is reviewed: real ones are fixed and listed in the CHANGELOG under **Security**; ones that don't apply are **dismissed** in Code scanning with a reason (for example "test-only code", "input already validated in X").
- A PR with a failing security check isn't merged unless the alert was dismissed with a reason.
- Reports from others: reply within 7 days (SECURITY.md), fix privately through a GitHub Security Advisory, publish after the patch is out.

## 5. Manual checks before each release

- [ ] The `build`, `codeql` and `security` workflows are green on the release commit.
- [ ] No open High/Critical alerts in Code scanning or Dependabot.
- [ ] Changes to the Practice frame, `ApiRouter`, `WebUi`, `MybkRunner`, `Paths` or updates: re-read the table in section 2 and note why in the PR.
- [ ] New LMS/MyBK actions: the button's JS was read and confirmed view-only, nothing is submitted.
- [ ] The personal-trace scan is clean when exporting to the public repository.
- [ ] Installers come with SHA256SUMS and an attestation.

## 6. Not done yet (current limits)

- Installers aren't code-signed yet (Windows shows a SmartScreen warning).
- No manual penetration test or fuzzing yet for `ApiRouter` and the quiz format readers.
- The private review repository has no Code scanning: checks still run and fail, but there is no SARIF.
