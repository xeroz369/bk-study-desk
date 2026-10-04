# BK Study Desk Privacy Policy

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](../../PRIVACY.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](PRIVACY.md)

> Translated from PRIVACY.md (Vietnamese) for BK Study Desk 1.1.4.

Updated: 3 October 2026

BK Study Desk is an unofficial app developed by an individual (xeroz369) for students of Ho Chi Minh City University of Technology – VNU-HCM (HCMUT). It is not part of the university and is not endorsed by the university or Moodle HQ. Every address and API the app calls is listed on the Wiki page [How the app works](https://github.com/xeroz369/bk-study-desk/wiki/C%C3%A1ch-app-ho%E1%BA%A1t-%C4%91%E1%BB%99ng) (Vietnamese).

## What the app collects

**The app sends no data to the developer.** It has no server of its own, no analytics and no ads. Apart from the university's servers, it only contacts GitHub if you allow update checks, and the study library site if you turn on **Library** (see below).

The app connects only to the HCMUT sites you already use:
- `sso.hcmut.edu.vn`: sign-in page;
- `lms.hcmut.edu.vn`: BK-LMS;
- `mybk.hcmut.edu.vn`: MyBK;
- other university services you open yourself;
- `github.com` / `api.github.com`: **only if you allow** update checks (**Settings** > **Updates**; nothing is checked until you choose). The app only asks for the latest version and downloads the update package; it sends no account information. As with any network connection, GitHub (servers in the US) sees your IP address, under [GitHub's privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement). Choose **Don't check** and the app does not contact GitHub.
- the shared study library site (BK Study Library): **only if you turn it on** in **Settings** > **Library** (off by default until the library opens). When on, the app downloads the library list (`index.json`, about once a day and when you open the **Library** tab), the material list of the course you are viewing, and the files you choose to **Download**, from the addresses the library lists for each item (possibly GitHub Pages or `raw.githubusercontent.com`). The app only sends ordinary download requests (HTTP GET): no student ID, name or course codes of yours, no cookies, no identifiers. As with any network connection, the server sees your IP address. Turn **Library** off and the app does not contact that site.

## Data stored on your PC

After you sign in, the app downloads and stores **on your PC**:
- deadlines, class timetable, exam schedule;
- course documents you choose to download (the app does not download them automatically by default);
- grades and curriculum progress;
- your name, student ID and class (for display);
- reviews of LMS quizzes **you have submitted** (for revision; can be turned off in Settings; quizzes in progress are never read);
- quizzes you write or import, practice results, notes;
- the time of your last SSO sign-in and when the session expired (`data\sso-session.json`, timestamps only), to learn how long the school keeps sessions;
- with **Library** on: a saved copy of the library list (`data\library-cache`), the list of files downloaded from the library (`data\library-downloads.json`), and the files you download, in each course's `Thư viện` folder. When an item is removed from the library, the app deletes the copy it downloaded (only if you have not edited that file). **Settings** > **Library** > **Clear library cache** deletes the saved list.

This data lives in `%LOCALAPPDATA%\BKStudyDesk.Data`, wherever you install the app. When uninstalling you choose whether to delete or keep it.

Course documents are saved to the folder you choose, by default `Documents\BK Study Desk`.

The app **does not store** your ID card number, address, phone number, date of birth or personal email, even when MyBK returns them.

The app writes an activity log to `data\app.log` on your PC (about 4 MB at most, rotated) for you to attach to bug reports. Tokens, cookies, sign-in tickets and email addresses are masked before writing; the log may contain subject names, file names and sync times. Open it from **Settings** > **Activity log**.

**Retention:** the data stays on your PC until you delete it (see Deleting data). Newer syncs overwrite older data; the app sends copies nowhere.

## Roles and legal basis

- Applicable law: Vietnam's [Personal Data Protection Law No. 91/2025/QH15](https://congbao.chinhphu.vn/van-ban/luat-so-91-2025-qh15-45578/57730.htm) and [Decree 356/2025/ND-CP](https://datafiles.chinhphu.vn/cpp/files/vbpq/2026/01/356-nd.signed.pdf) (both in force since 1 January 2026).
- **HCMUT** holds the source data (grades, schedules, records on LMS and MyBK). To correct or delete that data, contact the university.
- **You** use the app to view your own data on your own PC.
- **The developer** does not receive, store or have access to your data. In the author's reading, the developer is therefore neither a data controller nor a data processor under Article 2 of Law 91/2025. This is the author's interpretation and has not been confirmed by any authority.

## Your rights

Under Article 4 of Law 91/2025 you have the right to know, view, correct and delete your data. For data the app stores on your PC:
- **View:** everything the app has is shown in the app; the files are in `%LOCALAPPDATA%\BKStudyDesk.Data` (JSON, readable in any text editor).
- **Correct:** data from the university is corrected at the university; the app syncs the new data.
- **Delete:** see Deleting data.

## Other people's data

Downloaded data may contain other people's information (lecturers' names, classmates' posts on LMS forums). Use it only for your own studies; do not republish it, share it or put it in publicly shared quizzes (Articles 4 and 7 of Law 91/2025). Saved LMS quizzes can only be shared after the quiz has closed, and without names or student IDs.

## Sign-in and security

- Your password is typed only on the university SSO page, inside the in-app browser (WebView2). The app never reads it. If you choose **Save** when the sign-in window asks, WebView2 stores the password encrypted on your PC to fill it in next time.
- Sign-in cookies live in the WebView2 profile, encrypted by WebView2.
- The BK-LMS token is encrypted with Windows DPAPI; only your Windows account can decrypt it.
- The app only **reads** data. It never submits work, registers for or cancels courses, makes payments or takes quizzes for you.

## Deleting data

- In the app: **Settings** > **Sign out** deletes the token and sign-in cookies. Synced data (timetable, grades...) is kept for offline viewing.
- Delete all app data: quit the app, then delete the folder `%LOCALAPPDATA%\BKStudyDesk.Data`, or uninstall as below.
- Uninstall: Windows **Settings** > **Apps** > **BK Study Desk** > **Uninstall**, and select **Xóa cả dữ liệu của app** (Delete app data) in the uninstaller. Documents downloaded to your chosen folder are kept; delete them yourself if you wish.

## Contact

Questions or bug reports: [GitHub Issues](https://github.com/xeroz369/bk-study-desk/issues). Issues are public: do not post names, student IDs, grades, tokens or screenshots with personal information. Report security issues privately per [SECURITY.md](SECURITY.md). Private privacy questions: [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).
