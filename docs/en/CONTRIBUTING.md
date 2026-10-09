# Contributing

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](../../CONTRIBUTING.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](CONTRIBUTING.md)

> Translated from CONTRIBUTING.md (Vietnamese) for BK Study Desk 1.2.3.

Issues and pull requests are very welcome, especially when the university changes its pages or APIs. You can write issues in Vietnamese or English; keep technical terms in English.

## Where to write it

Decide where the code belongs before you write it; do not patch the nearest file.

| Task | Write it in |
|---|---|
| Read new data from LMS or MyBK | `BKStudyDesk.Core/Sources/Lms`, `Sources/Mybk` (one file per API group) |
| Records, saving data files | `BKStudyDesk.Core/Data/` |
| Pure rules (Vietnam time, urgency, group codes) | `BKStudyDesk.Core/Core/` or `Ui/Due.cs`, with tests |
| New config key | `BKStudyDesk.Core/Core/DefaultConfig.json` and a property in `Core/Settings.cs` |
| What a page shows, in which order, row text; the info bar; Settings items | `BKStudyDesk.Core/Presentation/` (pure, with tests) |
| Layout of a page | `BKStudyDesk.Desktop/Views/<Page>View.axaml` |
| Window frame, top bar, info bar, Notifications panel | `BKStudyDesk.Desktop/MainWindow.axaml(.cs)` |
| Sign-in, WebView, hidden MyBK browser | `BKStudyDesk.Desktop/Web/` |
| Tray, start with the computer, password store, Store package, system notifications | `BKStudyDesk.Desktop/Platform/` (`Platform/Windows/` builds only for Windows) |
| Practice: grading, review schedule, packs, import and export | `BKStudyDesk.Core/Presentation/Practice/`, with tests |
| Practice: screens, buttons | `BKStudyDesk.Desktop/Views/Practice/` |
| Colors, sizes, layout numbers | `BKStudyDesk.Desktop/Styles/Tokens.axaml`, rules in `design-system/bk/README.md` |
| UI text | `BKStudyDesk.Core/lang/vi.json` and `lang/en.json` |

If nothing fits, add a small new file that does one thing instead of growing a big one.

## Folder layout

```
src/
├── BKStudyDesk.Desktop/ the app (Avalonia 12, built-in Fluent theme; Windows, macOS, Linux)
│   ├── Views/           pages, dialogs, Practice (Practice/)
│   ├── Web/             sign-in, WebView, hidden MyBK browser
│   ├── Platform/        tray, start with the computer, password store, notifications (Windows/ only for Windows)
│   ├── Updates/         update check and download (Velopack)
│   └── Styles/          color and size tokens (Tokens.axaml)
├── BKStudyDesk.Core/    core, no UI dependency
│   ├── Core/            config, paths, log, pure rules
│   ├── Data/            records and data files
│   ├── Sources/         LMS (Lms/) and MyBK (Mybk/) readers
│   ├── Library/         document library
│   ├── Files/           placing and naming downloaded files
│   ├── Api/             internal API (Practice reads and writes results, packs)
│   ├── Presentation/    logic of each page, info bar, Settings, Practice
│   ├── Ui/              app state, formatting, text (no UI)
│   └── lang/            UI text vi.json, en.json
├── ThirdParty/XamlMath/ TeX formula renderer (MIT, maintained here)
└── BKStudyDesk.Setup/   Vietnamese installer and uninstaller
studypack/               study pack spec and tools (Node)
tests/                   xUnit: BKStudyDesk.Logic.Tests, BKStudyDesk.Core.Tests, BKStudyDesk.Math.Tests
```

## Layers

- `BKStudyDesk.Core` uses no Avalonia and no OS-specific API outside `OperatingSystem.IsWindows()` branches.
- Views do not filter, sort or decide what to show: they ask `Presentation/`. Views do not call the network: they go through `AppHost` and the services.
- No icons, no custom controls, no re-templated controls (`design-system/bk/README.md`).
- These are the target rules. Where code still breaks them, do not make it worse.
- `BKStudyDesk.Logic.Tests` picks up every file in the core's `Core/`, `Data/`, `Sources/` and `Library/` by itself. A file that needs `Config` or `Paths` goes into `LogicExclude` in `BKStudyDesk.Logic.Tests.csproj`, with the reason.

## Add a Settings item

1. Add the key to `Core/DefaultConfig.json` and a property to `Core/Settings.cs`.
2. Add one line to `Presentation/SettingsCatalog.cs` (`ToggleItem`, `NumberItem`, `ChoiceItem`...), in the right card; rarely used items go in the Advanced group. `SettingsView` draws it; no change needed there.
3. The item reads and writes only through `Settings`, never `Config` directly.
4. Add the UI text to `lang/vi.json`, then `lang/en.json`.

## Add a column to the event table

1. The column value is a property of the row type in `Presentation/` (for example `CalendarRow` in `Presentation/Calendar.cs`), built from `TimelineItem` (`Ui/AppState.cs`) and the records in `Data/Models.cs`; add a new field to both.
2. Rules for the value (urgency, group codes) are pure code in `Core/` or `Ui/Due.cs`, with tests.
3. Add the column to the `DataGrid` in `Views/<Page>View.axaml`: short columns `Width="Auto"` with a `MinWidth`, the name column takes the rest; never a fixed width that cuts text.
4. Add the column header to `lang/vi.json`, then `lang/en.json`.

## Before you open a pull request

```powershell
dotnet format src/BKStudyDesk.Core/BKStudyDesk.Core.csproj --verify-no-changes --severity warn
dotnet format src/BKStudyDesk.Desktop/BKStudyDesk.Desktop.csproj --verify-no-changes --severity warn
dotnet build src/BKStudyDesk.Desktop -c Release -f net10.0-windows10.0.19041.0 -warnaserror
dotnet test tests/BKStudyDesk.Logic.Tests
dotnet test tests/BKStudyDesk.Core.Tests
dotnet test tests/BKStudyDesk.Math.Tests
```

If you change the UI, take new screenshots with `--snap=<image.png>` (the app draws itself to an image, with demo data), light and dark (`--theme=light`). CI runs exactly these commands on Windows, macOS and Linux.

## Rules

- **Read-only:** no feature may submit work, register, cancel, pay or change any data on university systems.
- **No personal data** (names, student IDs, tokens, machine paths) in code, images, logs or issues.
- **Simple code:** short functions that do one thing, clear names. Comments explain *why*; English, or Vietnamese with English technical terms, are both fine.
- **UI text** lives in `src/BKStudyDesk.Core/lang/*.json`: write `vi.json` first, then `en.json`, and keep every `{0}` placeholder.
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
