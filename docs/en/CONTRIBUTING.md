# Contributing

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](../../CONTRIBUTING.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](CONTRIBUTING.md)

> Translated from CONTRIBUTING.md (Vietnamese) for BK Study Desk 1.1.3.

Issues and pull requests are very welcome, especially when the university changes its pages or APIs. You can write issues in Vietnamese or English; keep technical terms in English.

## Before you open a pull request

```powershell
dotnet format src/SoHocTap --verify-no-changes --severity warn
dotnet build src/SoHocTap -c Release -warnaserror
dotnet test tests/SoHocTap.Tests
```

If you change the Practice view (`src/ui`), also run `npm run check` and `npx prettier --check src`. CI runs exactly these commands.

## Rules

- **Read-only:** no feature may submit work, register, cancel, pay or change any data on university systems.
- **No personal data** (names, student IDs, tokens, machine paths) in code, images, logs or issues.
- **Simple code:** short functions that do one thing, clear names. Comments explain *why*; English, or Vietnamese with English technical terms, are both fine.
- **UI text** lives in `src/SoHocTap/lang/*.json`: write `vi.json` first, then `en.json`, and keep every `{0}` placeholder.
- **Style:** follow [docs/van-phong.md](../van-phong.md) (Vietnamese) and [docs/english-style.md](../english-style.md) (English) for terms, capitalization, punctuation, error messages and the changelog. Every rule there cites a source.
- **Two languages:** when you change the Vietnamese part of a document, update its English counterpart (the file with the same name in `docs/en/`, the English Wiki page) in the same pull request.
- **No guessing:** UI patterns, API limits, timeouts and similar numbers must come from primary sources (Microsoft Learn, source code, RFCs) or real measurements, cited in a comment. Anything the README or Wiki promises, the code must actually do.
- **Commits** are short and follow [Conventional Commits](https://www.conventionalcommits.org/): `feat: ...`, `fix: ...`, `docs: ...`.

## Testing

- **Automated tests** never call the university's real servers (LMS, MyBK, SSO) and never use real accounts, cookies or tokens.
- **Network and server failures** (429 with `Retry-After`, 5xx, timeouts, no connection, broken JSON) are simulated with a fake `HttpMessageHandler` or a mock server, using anonymized sample data (no names, student IDs or tokens).
- **Time-dependent logic** uses `TimeProvider`; tests don't `sleep`.
- **UI tests** find controls by AutomationId and use UI Automation patterns (Invoke, Toggle, Value), not screen coordinates.
- **Real checks on LMS/MyBK** are done by hand by the maintainer, read-only, before a release.

## License

By opening a pull request you agree to license your contribution under the project's license ([PolyForm Noncommercial 1.0.0](../../LICENSE.md)).
