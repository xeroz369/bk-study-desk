# Language packs

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](README.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](README.en.md)

> Translated from README.md (Vietnamese) for BK Study Desk 1.1.3.

All UI text comes from the language packs in this folder.

- Each language is one JSON file named after its language code:
  - `vi.json`: Vietnamese, the **source** and default language;
  - `en.json`: English.
- This folder is copied next to the exe, so you can edit or add packs without rebuilding the app.

## Format

Each pack is a flat JSON object of `"key": "text"` pairs, saved as UTF-8 with 2-space indentation:

```json
{
  "_meta.name": "English",
  "_meta.culture": "en-US",

  "nav.today": "Today",
  "download.summary": "{0} sections with documents · {1} selected, {2} files ({3})"
}
```

- **Keys** look like `group.name`, for example `nav.today`, `settings.save`. Translate only the values; **never change the keys**.
  - Groups: `app`, `nav`, `status`, `info`, `login`, `common`, `col` (table column headers), `home`, `calendar`, `subjects`, `download`, `grades`, `curriculum`, `services`, `settings`, `tray`, `notify`, `timeline`, `format`, `kind`, `files`, `about`, `data`, `link`, `setup`, `sync`, `update`, `web`.
- **`_meta.name`**: the language name shown in Settings.
- **`_meta.culture`**: the .NET culture used to format numbers and dates, for example `vi-VN`, `en-US`, `fr-FR`.
- **Placeholders** `{0}`, `{1}`…: the app fills in numbers, names or dates here (see [`string.Format`](https://learn.microsoft.com/dotnet/standard/base-types/composite-formatting)).
  - Keep every placeholder from the source; you can change their order.
  - Some placeholders carry a format:
    - `{0:0}`: whole number;
    - `{1:dd/MM}`: day/month;
    - `{0:N0}`: with thousands separators.
  - To show a brace, write `{{` or `}}`.
- `_` marks the Alt access key, for example `"_Filter"` is Alt+F.
- `\n` is a line break, `\"` is a double quote.

## Missing keys

- If the selected pack lacks a key, the app uses the Vietnamese text from `vi.json`.
- If `vi.json` lacks it too, the app shows the key itself, for example `settings.save`, so missing text is easy to spot.
- A broken pack (invalid JSON) is skipped and the app uses Vietnamese; the reason is written to `data\app.log`.

## Add a language

1. Copy `vi.json` (or `en.json` if that's easier to translate from) to `<code>.json`, for example `fr.json`.
2. Set `_meta.name` (for example `"Français"`) and `_meta.culture` (for example `"fr-FR"`).
3. Translate the values. Keep the keys and the `{0}`, `{1}`… placeholders.
4. Reopen the app.
5. Go to **Settings** > **Language**, select the new language, then select **Restart now**.

The choice is saved in `data\config.json` under the key `app.language`. Pull requests that add languages are very welcome.

**When you add or change UI text:** edit `vi.json` first, then add the translation to the other packs. English text follows [docs/english-style.md](../../../docs/english-style.md).
