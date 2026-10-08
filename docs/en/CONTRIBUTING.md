# Contributing

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](../../CONTRIBUTING.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](CONTRIBUTING.md)

> Translated from CONTRIBUTING.md (Vietnamese) for BK Study Desk 1.1.4.

Issues and pull requests are very welcome, especially when the university changes its pages or APIs. You can write issues in Vietnamese or English; keep technical terms in English.

## Where to write it

Decide where the code goes before you write it; do not patch it into the nearest file.

| Task | Write it in |
|---|---|
| Read new data from LMS or MyBK | `Sources/Lms`, `Sources/Mybk` (one file per API group) |
| Records, saving data files | `Data/` |
| Pure rules (Vietnam time, urgency level, splitting group codes) | `Core/` or `Ui/Due.cs`, with tests |
| New config key | `Core/DefaultConfig.json` and one property in `Core/Settings.cs` |
| Settings card | one file in `Ui/Settings/`, one line in `SettingsSections.cs` |
| Column or display style of the event table | `Ui/Controls/TimelineList` |
| New page | `Ui/Pages/`, registered in `PageRegistry` |
| Windows, tray, sign-in, WebView | `Shell/` |
| Practice in the cross-platform app: grading, review schedule, packs, import and export | `BKStudyDesk.Core/Presentation/Practice/`, with tests |
| Practice in the cross-platform app: screens, buttons | `BKStudyDesk.Desktop/Views/Practice/` |
| Display text | `lang/vi.json` and `lang/en.json` |

If nothing fits, create a small new file that does one thing instead of growing a large one.

## Folder layout

```
src/
├── SoHocTap/            WPF app (.NET 10, Fluent theme)
│   ├── Core/            config, paths, log, pure rules
│   ├── Data/            records and saving data files
│   ├── Sources/         reading LMS (Lms/) and MyBK (Mybk/)
│   ├── Library/         document library
│   ├── Files/           sorting and naming downloaded files
│   ├── Updates/         checking for and downloading updates
│   ├── Api/             internal API for the Practice view
│   ├── Shell/           windows, tray, reminders, sign-in
│   ├── Ui/              WPF UI (Pages/, Controls/)
│   ├── lang/            UI text: vi.json, en.json
│   └── tools-dev/       release and export scripts
├── ui/                  Practice view of the WPF app (Svelte), built into ui/
├── BKStudyDesk.Core/    cross-platform core: files linked from SoHocTap and the Practice logic
├── BKStudyDesk.Desktop/ Avalonia app (Windows, macOS, Linux); Practice drawn with built-in controls
├── ThirdParty/XamlMath/ TeX formula renderer for the Avalonia app (MIT, maintained here)
└── BKStudyDesk.Setup/   installer
tests/                   xUnit: SoHocTap.Tests, BKStudyDesk.Core.Tests, BKStudyDesk.Math.Tests
```

`Ui/Format.cs`, `Ui/AppState.cs`, `Ui/Lang.cs`, `Ui/Models.cs`, `Ui/Curriculum.cs` and `Ui/Due.cs` are also compiled into `BKStudyDesk.Core`, so keep them free of WPF.

## Layers

- `Core/`, `Data/`, `Sources/`, `Library/`, `Files/` and `Updates/` do not use WPF.
- `Ui/` does not call the network directly: it goes through `AppHost` and the services.
- `Shell/` does not depend on concrete pages: pages register through `PageRegistry`.
- This is the target rule. Where code still breaks it, do not make it worse.
- Tests pick up every file in `Core/`, `Data/`, `Sources/` and `Library/` by themselves. A file that needs `Config`, `Paths` or WPF goes into `LogicExclude` in `SoHocTap.Tests.csproj` with a reason.

## Add a Settings card

1. Add the key to `Core/DefaultConfig.json` and a property to `Core/Settings.cs`.
2. Create `Ui/Settings/<Name>Section.xaml` and `.xaml.cs`, implementing `ISettingsSection`.
3. The card reads and writes only through `Settings`, never `Config` directly.
4. Add one line to `Ui/Settings/SettingsSections.cs`: `Common` if people need it often, `Advanced` (the collapsed Advanced section) if rarely.
5. Add the display text to `lang/vi.json`, then `lang/en.json`.

## Add a column to the event table

1. The column lives in `Ui/Controls/TimelineList`, shared by every event table.
2. The column reads a property of `TimelineItem` (`Ui/AppState.cs`), built from the records in `Data/Models.cs`; add a new field to both.
3. Write the rule that computes the value (urgency, group split) as pure code in `Core/` or `Ui/Due.cs`, with tests.
4. Add the column header to `lang/vi.json`, then `lang/en.json`.
5. Create the column with `Grids.Text`, `Grids.Flex` or `Grids.Right` so the width scales with the chosen font size; never set a fixed width that hides text (DESIGN 6b-3).

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

By opening a pull request you agree to license your contribution under the project's license ([AGPL-3.0](../../LICENSE.md)).
