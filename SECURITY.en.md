# Security policy

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](SECURITY.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](SECURITY.en.md)

> Translated from SECURITY.md (Vietnamese) for BK Study Desk 1.1.3.

## Reporting a vulnerability

**Do not report security vulnerabilities in public issues.** Report them privately via [**Security** > **Report a vulnerability**](https://github.com/xeroz369/bk-study-desk/security/advisories/new), or email [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).

Please include:
- the app version and Windows version;
- steps to reproduce;
- the impact: leaked token or session, running untrusted code, writing files outside the app folder, sending requests that change data on LMS/MyBK...

You will get a reply within 7 days. The fix ships in the next release, with credit to the reporter if you wish.

## Supported versions

Only the latest version on [Releases](https://github.com/xeroz369/bk-study-desk/releases) receives security fixes. Turn on updates in **Settings** > **Updates** to stay current.

## How the app protects you

- Your password is typed only on the university SSO page; the app never reads it. If you choose **Save** when asked, WebView2 stores it encrypted on your PC for autofill.
- The LMS token is encrypted with Windows DPAPI; data lives in `%LOCALAPPDATA%`, readable only by your Windows account (plus SYSTEM and Administrators).
- The app only calls **read** APIs; it never submits, registers, cancels or pays.
- Update packages are verified with SHA-256 before installing; the update source must be the GitHub repository over https (a local folder is accepted only in test installs).
- Quiz packs from other people are validated and their HTML is sanitized; no scripts run and no external images load.
- The source is scanned automatically with CodeQL; dependencies are monitored for vulnerabilities by Dependabot.
