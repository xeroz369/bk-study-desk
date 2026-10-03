# Changelog

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](../../CHANGELOG.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](CHANGELOG.md)

> Translated from CHANGELOG.md (Vietnamese) for BK Study Desk 1.1.9.

Notable changes to BK Study Desk. The format follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and versions follow [SemVer 2.0.0](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.9] - 2026-10-03

### Added

- Shared study **Library** (BK Study Library), **off by default until the library opens**. Turn it on in **Settings** > **Library** and the **Subjects** page gets a **Library** tab for courses whose code matches: material grouped by type, with filters for teacher, term and exam type. Each item offers **Open on the web**, **Download** (to the course's `Thư viện` folder, checked before saving, never overwriting your files), **Add to Practice** for quiz packs, and **Copy link**. A **Contribute material** button opens the contribution page on the web. Items removed from the library are hidden and the app deletes the copy it downloaded.

### Changed

- Downloading an update shows a progress bar with the real percentage and the amount downloaded (e.g. "Downloading version 1.2.0... 42% (12.3/29.1 MB)"), in the update window, **Settings** > **Updates** and the status bar (also when **Automatic** mode downloads in the background). The bar never moves backwards and only fills up once the package is downloaded and checked. There is a **Cancel download** button.
- Installing an update no longer shows Velopack's English "Installing Update" window with its endless bar. The app says "Installing version ..., the app will reopen in a few seconds" in the status bar, then reopens by itself.
- The sync progress bar keeps the percentage reached while a step takes long, instead of switching to a back-and-forth bar.

### Fixed

- **Calendar** > **Timetable**: the current time line disappeared in the evening or early morning, because the grid only spanned your earliest to latest class ([#25](https://github.com/xeroz369/bk-study-desk/issues/25)). The grid now always covers 0:00 to 24:00 and scrolls to your earliest class, or to the current time when it is out of view.
- **Calendar** and **Today**: deadlines and quizzes set more than 120 days ahead on LMS now show too (the app used to read the LMS calendar only 120 days ahead). **Today** lists every LMS announcement from the last 7 days, matching the **LMS announcements (7 days)** count (it used to stop at 8).
- MyBK sometimes failed to sync with "no response after 45 seconds (at .../my/homeSSO.action)" while the school server was fine. It happened when the MyBK session had expired and the app had to sign in again through SSO.

## [1.1.8] - 2026-10-03

### Added

- **Calendar** > **Timetable** grid: an accent-coloured line marks the current time in today's column ([#22](https://github.com/xeroz369/bk-study-desk/issues/22)).
- **Add event...** on the **Calendar** page: add your own events or makeup classes ([#22](https://github.com/xeroz369/bk-study-desk/issues/22)). Type one line such as "Học bù Giải tích 2 - 12/10 - 7:00-8:50 - H1-201" and the form fills itself. Your events show in the week grid (dashed border), **Upcoming**, **Today**, the .ics export, and get a reminder about an hour before. Right-click to edit, delete or copy. Stored only on your computer.
- **Settings** > **Updates**: choose how often to check for a new version (6, 12 or 24 hours).

### Changed

- The sync progress bar shows real progress step by step (e.g. "LMS: Đang tải tệp 2/5 (Giải tích 2)..."), never moves backwards, and doesn't flash for short syncs.
- A sync with nothing new writes no files, redraws nothing and sends no notification. Due-date reminders are not repeated, even after restarting the app.
- The **Subjects** page opens faster: folders are scanned in the background and only the open tab is loaded; junk files and hidden folders (`.history`, `~$...`, `desktop.ini`...) are skipped.
- The **About** button is icon only.
- Checks for a new version every 6 hours (was 24), and when the PC wakes up or the network returns. **Automatic** mode installs a downloaded update the next time the app starts; the status bar then says "Updated to ...".

### Fixed

- When LMS updated a file whose content matched a file you had saved elsewhere in the course folder, the app could move **your file** to `_Lưu trữ\Bản cũ`. The app now only replaces files inside `Tài liệu LMS`.
- Course names with accents stored in a different Unicode form (common for folders created on another computer) didn't match the LMS course.
- A course's **Upcoming** tab could show deadlines of another course whose name started the same way.

## [1.1.7] - 2026-10-03

An experimental release: most of the code has been reorganised to make features and fixes easier. If something looks wrong, please open an issue.

### Security

- Automated security checks on every change and every week: OSV-Scanner, npm audit, NuGet advisories, gitleaks, zizmor, Semgrep, BinSkim, OpenSSF Scorecard (alongside the existing CodeQL and Dependabot). Process and response times: [security-process.md](security-process.md).

### Changed

- Narrow windows (1024 px or less): the navigation bar shows icons only instead of overflowing, so every page stays reachable.
- Tables keep their main column wide enough in narrow windows; long column names end with an ellipsis and a tooltip; tab rows no longer wrap onto two lines.
- A finished sync no longer resets sorting, scroll position and the selected row. Only the page you are looking at is refreshed.
- **Practice** frees its memory (about 300 MB) after you leave it for 3 minutes or when the app goes to the tray, and comes back when you open it again.
- **Random exam** keeps the original question order and keeps related questions together (new `group` field in Study Pack).
- **Practice** right-click menus use the same command names everywhere; chapter headers have a right-click menu.
- Sync skips a run while offline instead of reporting an error, and syncs soon after the network returns or the PC wakes up; in battery saver mode the interval is longer.
- Sync error messages follow the kind of error (session expired, offline, server error...), now also in English.
- When one part of the MyBK data fails, the previous data for that part is kept instead of being emptied. Unreadable data is reported instead of showing an empty page.
- Settings: enter 0 in a reminder box to turn that reminder off; the MyBK sync interval is at least 6 hours.
- The log file records full error details and environment info, which helps when reporting bugs.

### Fixed

- When the MyBK session expired, MyBK sync hung for 45 seconds and then reported "MyBK did not respond" (a bug in 1.1.5 and 1.1.6). The app signs in again through SSO as before.
- Empty pages without an explanation: the **Subjects** tabs, **Calendar** > **Upcoming**, **Exams**, and the **Subjects** card in **Practice**.
- Cut-off text: the "stay signed in" box in **Settings** and the error line in the **Download** dialog; the status bar pushed out the exam name and version.
- GPA mixed dots and commas; the tuition column wasn't formatted as money; copying in **Calendar** didn't say "Copied".
- **Practice**: errors when importing, saving or removing a pack are no longer silent; hints in input boxes no longer mention another course's chapter.
- Saving a file that is open in another program (for example a PDF) no longer breaks the whole LMS sync.
- With several monitors, the window remembers its position on the right monitor.
- The app no longer runs an extra SSO keep-alive right after a MyBK sync.

## [1.1.6] - 2026-10-03

### Fixed

- MyBK sync reported "the registration page has no registration rounds table". On the first visit to the registration system, MyBK returns an intermediate page before the page with the table, and the app read it too early. The app now waits and opens the page again when the table is missing.
- Every LMS assignment showed up twice on **Today**, **Calendar** and in due-date reminders, so the "due in 7 days" count was doubled.
- Submitted assignments still triggered reminders. The app now follows the Moodle calendar: Moodle removes the entry once you submit, so the app no longer counts or reminds you about it. The brief and attachments are still saved to your computer as before.

## [1.1.5] - 2026-10-03

### Fixed

- In **Practice**, the app shortcuts (Ctrl+1 to Ctrl+7, F5, Alt+Left) did nothing, and links to other app pages did not open.
- MyBK sync failed with "MyBK redirected to a page outside the university (mybk.hcmut.edu.vn/app/login)" when the MyBK session expired. MyBK sends you to its sign-in page over http and the app blocked it by mistake. The app now switches to https and signs in again through SSO.
- **Calendar** > **Timetable** grid: weeks without Saturday or Sunday classes dropped those columns, although the week label runs to Sunday. The grid now always shows all 7 days, and the scrollbar no longer covers the last column.

## [1.1.4] - 2026-10-03

### Added

- The **Registration & records** tab has a **Lecturers and class times this term** table, grouped by lecturer: subject, code, class group, day, time, room ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)). The lecturer and time columns moved here from the **Registration results** table.
- After you sign in again through **Open MyBK** / **Open LMS** on the error bar, the app syncs that source again without pressing **Sync** ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- **Practice**: right-click menus on quiz packs, courses and lessons have every action: open, create a question, import into the course, export to share (**Xuất để chia sẻ**), remove.

### Changed

- **Practice**: the **Soạn** (Write) button is renamed **Tạo** (Create); **Tạo**, **Nhập** and **Xuất** (Create, Import, Export) are three separate buttons.
- Interface text drops decorative characters (middle dots, ellipses, arrows) in favour of commas and plain words.

- English documents moved to `docs/en/`; the repository root keeps only the Vietnamese versions. The **About** page opens `docs/en/PRIVACY.md` when the interface is in English.
- Documents (README, PRIVACY, SECURITY, CONTRIBUTING, CHANGELOG, code of conduct, studypack) use a row of **Tiếng Việt** | **English** badges at the top to switch language, following the multilanguage-readme-pattern.

### Fixed

- The **Download** dialog did not open in 1.1.3.
- **Practice**: removing a quiz pack, deleting a question, and updating or replacing an installed pack did nothing (the confirmation never appeared). A Windows dialog now asks.
- **Practice**: the selected tab (Tạo, Nhập, Xuất, Nhờ AI) looked the same as the others.
- **Calendar** > **Upcoming**: the right-click menu had two identical open items.
- The registration rounds table was empty when MyBK redirected to its home page instead of the registration page. The app now opens the right page again, reports an error if the table is still missing, and does not save an empty result.

## [1.1.3] - 2026-10-03

### Added

- **Calendar** > **Timetable** has a **week grid** view like Google Calendar ([#5](https://github.com/xeroz369/bk-study-desk/issues/5)). The grid lays itself out: Saturday and Sunday appear only when there are classes, the hours span the earliest to the latest class, and overlapping classes share the column. The list view is still there; pick it under **View**.
- **Export calendar (.ics)**: saves the whole-term timetable and exams as an iCalendar file to import into Google Calendar, Outlook or Windows Calendar.
- When an LMS or MyBK sync fails, the error bar has an **Open MyBK** / **Open LMS** button to see what the page shows or to sign in again ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- **Download** lets you choose file types: PDF, slides (.ppt, .pptx), other; the file count and size follow your choice ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- The course registration table shows the lecturer and class times, taken from the MyBK timetable ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- English documents are separate, complete translations: `README.en.md`, `PRIVACY.en.md`, `SECURITY.en.md`, `CODE_OF_CONDUCT.en.md`, `CONTRIBUTING.en.md`, `CHANGELOG.en.md`, the studypack documents, and English Wiki pages. English style guide: `docs/english-style.md`.
- The **About** page opens the English privacy policy when the interface is in English.

### Changed

- The README and Wiki recommend the GitHub installer first and the Microsoft Store second (Store versions wait for Microsoft review, so they usually lag behind).

### Fixed

- The timetable list showed a "Thứ 0" (day 0) group with odd times such as "0:00–6:50". Courses without a fixed time are now listed separately under "No fixed time".

## [1.1.2] - 2026-10-03 [YANKED]

Withdrawn because this build had a support section inside the app. All changes moved to 1.1.3.

## [1.1.1] - 2026-10-03

### Changed

- UI and document wording follows the style guide `docs/van-phong.md`: "Sao chép" instead of "Copy", "Chọn" instead of "Bấm", "tải xuống" instead of "tải về"; error messages say what to do.
- Practice: "Các môn" was renamed "Môn học" (Subjects).

### Added

- README and Wiki: how to install from the Microsoft Store, with a note to check the version number because Store versions wait for Microsoft review.
- New screenshots (demo data) for the README, Wiki and Store page.
- Official project contact: bkstudydesk@xerozsoft.com (README, SECURITY, PRIVACY, code of conduct).

### Fixed

- A **quiz opening** on LMS was shown like a deadline ("2 days left" counted to the opening time). It now reads "Quiz opens" with the closing date and no longer counts toward "Quizzes in 14 days".
- MyBK sync reported "didn't respond after 45 seconds (at blank)" and every **Retry** failed the same way:
  - If opening MyBK is cancelled or fails, the app reports it immediately, with the reason.
  - After an error the app discards the hidden WebView, so the retry uses a new one.
  - The log records when WebView2 fails or when MyBK redirects to a page outside the university.
- Empty tables (LMS gradebook, transcript, curriculum, registration, community service, academic decisions) showed collapsed columns with no text. Empty tables now say why: syncing, sync failed, or genuinely nothing.
- Clicking a tab or a navigation item drew a white frame around it. The focus frame now appears only when you use the keyboard.

## [1.1.0] - 2026-10-03

### Added

- Available on the Microsoft Store: no SmartScreen warning, and the Store updates the app.
- The status bar shows the current sync step (for example "LMS: reading quizzes...", "checking classes... (3/5)").
- Errors appear in a standard Windows InfoBar: an icon for the severity, a plain sentence with what to do, and **Details** with the server's original text (with a **Copy** button). The status bar shows an error icon next to LMS/MyBK.
- The LMS/MyBK windows tell you when a page takes more than 10 seconds, the network is down, the server fails or the session has ended, with a **Retry** button.
- Keeps the sign-in session alive while the app is open: one SSO visit every 60 minutes (can be turned off in Settings). The university's SSO session still ends at most 8 hours after you sign in.
- **Settings** > **Activity log**: opens the log file; turn on diagnostic logging when reporting a bug (it turns itself off after 7 days).

### Changed

- By default the app reads only this term's courses and does not download documents automatically. To download, go to **Subjects** > **Download** and pick the sections; turn auto-download back on in Settings if you want.
- After the first sign-in, the calendar, deadlines, quizzes and grades appear right away instead of waiting for documents.
- Fewer requests: forum announcements only for this term's classes; quizzes without a closing time are checked once a day; the Subjects page scans folders only while it is open.
- Requests to LMS and MyBK are spaced out. When a server reports overload (HTTP 429/503), the app waits per `Retry-After` before trying again.
- **Stay signed in after closing the app** now keeps the session for at most 8 hours, the university server's limit (it used to say 30 days, but the server had ended the session long before).
- `data\config.json` stores only what you changed from the defaults.
- Pages that are empty because there is no data yet say whether it's still loading, failed or you're not signed in.

### Fixed

- The LMS calendar could not be read (the app asked for more than 50 items at once; it now reads page by page).
- Partial read errors (one subject's gradebook, one MyBK API, a file download) used to be ignored silently; they now appear in the error bar and the log.
- Sync reported "Access denied" when the UI was reading data while a sync was writing it.
- The mouse wheel got stuck over text boxes and tables; the "Letter grade" and "Scale" column headers were cut off; community service dates and day counts were formatted wrongly.
- The main window and the sign-in window ran off small screens.
- The **Settings** and **About** buttons did not respond to screen readers.

### Removed

- The `downloads.watch` config setting (it had no effect).

### Security

- The update source must be the GitHub repository over https; a local folder is used only for test installs.
- The log masks tokens, cookies, CAS sign-in tickets and email addresses before writing.
- WebView2's general autofill is off. Passwords are saved only when you choose **Save** when the in-app browser asks.

## [1.0.6] - 2026-10-02

### Added

- Vietnamese installer: choose the install folder, no admin rights needed. The uninstaller asks whether to keep or delete app data.
- Automatic updates, your choice: notify when a new version is out, install when you quit the app, or don't check.
- Practice: write quizzes, ask AI to write them, import/export Markdown, review submitted LMS quizzes, mock exams.
- Import data from the old zip version in Settings.

### Changed

- App data lives in `%LOCALAPPDATA%\BKStudyDesk.Data`.
- The license changed to PolyForm Noncommercial 1.0.0 (no commercial use). Earlier versions remain under MIT.

### Fixed

- Opening LMS/MyBK after the session expired showed "session timed out".

### Security

- Fixed several minor security issues (navigation limits for the hidden WebView, downloaded files are never run, file size limits).

Earlier versions: see [Releases](https://github.com/xeroz369/bk-study-desk/releases).

[Unreleased]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.9...HEAD
[1.1.9]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.8...v1.1.9
[1.1.8]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.7...v1.1.8
[1.1.7]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.6...v1.1.7
[1.1.6]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.5...v1.1.6
[1.1.5]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.4...v1.1.5
[1.1.4]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.3...v1.1.4
[1.1.3]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.1...v1.1.3
[1.1.2]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/xeroz369/bk-study-desk/compare/v1.0.6...v1.1.0
[1.0.6]: https://github.com/xeroz369/bk-study-desk/releases/tag/v1.0.6
