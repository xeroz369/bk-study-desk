# English writing style

[Văn phong tiếng Việt](van-phong.md) | **English**

How to write the English documents (`*.en.md`, English Wiki pages, the English half of release notes) and the English UI (`src/SoHocTap/lang/en.json`). Vietnamese is the source language; English documents are complete translations, not summaries.

The main reference is the **Microsoft Writing Style Guide**, because this is a Windows app and Windows uses its terms. The Google developer documentation style guide fills gaps.

## Sources

| Short | Source |
|---|---|
| MSG | [Microsoft Writing Style Guide](https://learn.microsoft.com/en-us/style-guide/welcome/) |
| MSW | [Windows app writing style](https://learn.microsoft.com/en-us/windows/apps/design/style/writing-style) |
| GDG | [Google developer documentation style guide](https://developers.google.com/style) |
| GTW | [Google technical writing: error messages](https://developers.google.com/tech-writing/error-messages) |

## Voice

- Address the reader as **you**. Use **we** only for the people who make the app. The app never says **I**. (GDG [person](https://developers.google.com/style/person), MSW)
- Use the **present tense**: "The app syncs every 3 hours", not "will sync". (GDG [tense](https://developers.google.com/style/tense))
- Use the **active voice** and start instructions with a verb: "Restart the app to apply the change." (MSW, MSG [top 10 tips](https://learn.microsoft.com/en-us/style-guide/top-10-tips-style-voice))
- Use common **contractions**: it's, you're, don't, can't. Don't invent new ones. (MSG, GDG [contractions](https://developers.google.com/style/contractions))
- Avoid **please**, except when the user has to do something inconvenient or the app is at fault. (MSG [please](https://learn.microsoft.com/en-us/style-guide/a-z-word-list-term-collections/p/please))
- Avoid **simply, easy, just, e.g., i.e.** Write "for example" and "that is". (GDG [word list](https://developers.google.com/style/word-list))
- Write **global English**: short sentences, one term for one idea, no idioms. Spell out an abbreviation the first time: "BK-LMS (the university's Moodle site)". (GDG [translation](https://developers.google.com/style/translation))
- Only say what the code really does. Check absolute claims ("never", "always") against the code before writing them. This is the same rule as the Vietnamese guide.

## Headings and structure

- **Sentence case**: capitalize only the first word and proper nouns. "Install the app", not "Install The App". No period or colon at the end. (MSG, GDG [headings](https://developers.google.com/style/headings))
- Task headings use a bare verb ("Update the app"); concept headings use a noun phrase ("Security and privacy"). (GDG)
- Put the language badges on the first line under the title (pattern: [multilanguage-readme-pattern](https://github.com/jonatasemidio/multilanguage-readme-pattern)): `[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](X.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](X.en.md)`.

## UI text in documents

- **Bold** UI labels, spelled exactly as in `en.json`: **Settings**, **Sign in to HCMUT**. Drop the trailing `...` or `:` of a label. Don't add the word "button" unless it's needed. (MSG [formatting text in instructions](https://learn.microsoft.com/en-us/style-guide/procedures-instructions/formatting-text-in-instructions), GDG [UI elements](https://developers.google.com/style/ui-elements))
- Menu paths use `>` with spaces, and the `>` is not bold: **Settings** > **Updates**. (MSG)
- **Input-neutral verbs**: *select* (buttons, items, checkboxes), *clear* (a checkbox), *enter* (type a value), *open*, *close*, *go to*, *turn on* / *turn off* (toggles). Not *click*, *tap* or *hit*. (MSG [describing interactions with UI](https://learn.microsoft.com/en-us/style-guide/procedures-instructions/describing-interactions-with-ui))
- Parts of the app that exist only in Vietnamese (the **Luyện tập** practice view, the installer and uninstaller): bold the Vietnamese label and add the meaning in parentheses: **Xóa cả dữ liệu của app** (Delete app data). This is a project choice.

## Numbers, dates, times

- Spell out zero to nine in text; use numerals from 10. Use a comma from four digits (1,024). Don't start a sentence with a numeral. (MSG [numbers](https://learn.microsoft.com/en-us/style-guide/numbers))
- Dates in prose: "October 13, 2026". Never "13/10/2026" in English, because readers can't tell which part is the day. In CHANGELOG headings use ISO `2026-10-13` (Keep a Changelog). (MSG [date and time terms](https://learn.microsoft.com/en-us/style-guide/a-z-word-list-term-collections/term-collections/date-time-terms), GDG [dates and times](https://developers.google.com/style/dates-times))
- Times: "10:45 AM". Add "UTC+7 (Vietnam time)" when the time zone matters.
- Use units with a space: 250 MB, 45 seconds.
- Use the Oxford comma: "PDF, slides, and other files". (MSG)

## Error messages

Say what happened, why (if known), and what the user can do. Don't blame the user. End with a period. Put technical text (error codes, the server's own message) under **Details** and in the log, not in the main sentence. (MSW, GTW)

- Yes: "Couldn't load the LMS calendar. Select **Retry** in a few minutes."
- No: "Error: core_calendar_get_action_events_by_timesort failed!"

## Buttons and dialogs

- Buttons are one or two words and use an active verb: **Install now**, **Retry**. No period. (MSW)
- In the UI, a button that opens another window ends with an ellipsis (for example "Export calendar (.ics)..."). In documents, write the label without it: **Export calendar (.ics)**.
- The buttons must answer the question in the dialog title. (MSW)

## Characters to avoid (project choice)

Documents, release notes, commits, pull requests, issue replies and the Wiki avoid characters that are common in AI-written text: em dash `—` (use a comma, colon, period or parentheses), arrows `→ ⇒ ↔` (use words; menu paths use `>`), the middle dot `·` as a separator (use a comma, `/` or `|`), the ellipsis character `...` (type `...`), curly quotes (use straight quotes) and emoji or decorative symbols. UI text from the app and the quiz file syntax stay as they are, because they must match the app.

## Links

- Write descriptive link text. Never "click here" or "this link", and don't use a bare URL as the link text. Use "For more information, see [Installation](Installation)." (GDG [link text](https://developers.google.com/style/link-text))

## Terms

| Use | Not | Note |
|---|---|---|
| sign in, sign out | log in, login (verb) | Windows term (MSG) |
| select | click, tap | input-neutral (MSG) |
| app | application, program | |
| folder | directory | Windows term |
| BK-LMS, MyBK, HCMUT | translated names | product and school names stay as they are |
| quiz, deadline, timetable | | same terms as the English UI |
| student ID | MSSV | explain Vietnamese terms the first time |

## Keeping translations in sync

- Vietnamese is the source of truth. Fix the Vietnamese document first, then the English one, in the **same pull request**.
- Each English document has a line under the switcher: `> Translated from README.md (Vietnamese) for BK Study Desk 1.1.2.` When the Vietnamese file changes and the English one is updated, bump the version in that line. (The idea comes from MDN's `l10n.sourceCommit`; the exact format is a project choice.)
- A pull request that changes a Vietnamese document but not its English counterpart must say why in the description.
