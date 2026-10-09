[Tiếng Việt](Cách-app-hoạt-động) | **English**

> Translated from Cách-app-hoạt-động (Vietnamese) for BK Study Desk 1.1.4.

This page lists exactly what the app sends to the university's servers, so anyone can check it. The matching source code is in `src/BKStudyDesk.Desktop/Web` and `src/BKStudyDesk.Core/Sources`; addresses and APIs are in `src/BKStudyDesk.Core/Core/DefaultConfig.json`.

> BK Study Desk is **unofficial**: it isn't endorsed by HCMUT or Moodle HQ. The app uses only your own account and only reads. If the university asks, the project will change or remove the feature concerned.

## 1. HCMUT sign-in (SSO)

- The app opens the university SSO page (`sso.hcmut.edu.vn`, a CAS system) in a window of the in-app browser (WebView2). You type your account and password yourself; the app never reads the password.
- After you sign in, the SSO server sets a session cookie. The cookie stays in WebView2's data folder on your PC.
- The university server decides how long the SSO session lasts: it ends sessions on its own schedule and after a long idle time. The app keeps the sign-in cookies and extends them on every pass through SSO, so it never removes a session earlier than the server does. While the app is open, it reopens the SSO entry page every 60 minutes so the session isn't ended for inactivity (can be turned off in Settings). Once the server has ended the session, sign in once more.

## 2. BK-LMS (Moodle)

The app uses **Moodle's web services**, like the official Moodle mobile app (the university tells students to use that app with BK-LMS).

**Getting a token.** The app opens Moodle's sign-in address for mobile apps:

```
https://lms.hcmut.edu.vn/admin/tool/mobile/launch.php?service=moodle_mobile_app&passport=<random string>&urlscheme=moodlemobile
```

- This flow is built into Moodle (open source: `admin/tool/mobile/launch.php`). Because you already signed in to SSO in step 1, you don't type your password again.
- `urlscheme=moodlemobile` is the URL scheme of the official Moodle app. By default Moodle returns tokens only to this scheme (the `forcedurlscheme` setting), so the app uses that scheme and **catches the `moodlemobile://token=...` address inside its embedded window**. The app doesn't register this scheme with Windows and doesn't impersonate the Moodle app's name or user agent.
- The app checks the token's signature (MD5 of the site address and passport), then stores the token in `data\secrets\`, encrypted with Windows DPAPI.

**Calling the API.** Every call is a POST to `https://lms.hcmut.edu.vn/webservice/rest/server.php` with the token. Only these **read** functions are used:

| Function | Used for |
|---|---|
| `core_webservice_get_site_info` | account name, token check |
| `core_enrol_get_users_courses` | list of classes |
| `core_course_get_updates_since` | which classes changed since last time |
| `core_course_get_contents` | class structure, list of documents |
| `core_calendar_get_action_events_by_timesort` | calendar, deadlines (50 items per call) |
| `mod_assign_get_assignments` | assignments |
| `mod_quiz_get_quizzes_by_courses`, `mod_quiz_get_user_attempts`, `mod_quiz_get_attempt_review` | quizzes, submitted attempts, the review page of a **submitted** attempt |
| `mod_forum_get_forums_by_courses`, `mod_forum_get_forum_discussions` | forum announcements |
| `message_popup_get_popup_notifications` | notifications |
| `gradereport_user_get_grade_items` | gradebook |
| `core_group_get_course_user_groups` | your class groups (to show only your group's deadlines) |

Documents and images in quiz questions are downloaded through `webservice/pluginfile.php` with the token. Documents are downloaded only when you choose the sections (or when you turn on auto-download in Settings).

The app **doesn't** call any function that submits work, starts or saves a quiz attempt, marks anything as viewed, or changes anything on LMS.

## 3. MyBK

- The app opens MyBK in a hidden WebView2 window with your SSO session: `sso.hcmut.edu.vn/cas/login?service=.../my/homeSSO.action`, then `/app/login?type=cas`, then `/app/`.
- MyBK's `/app/` page holds the page's own sign-in token. The app runs `fetch()` **inside that page** to call the JSON APIs the MyBK page uses; the token never leaves the page.
- APIs (paths after `https://mybk.hcmut.edu.vn/api/`):

| API | Data |
|---|---|
| `v1/student/get-student-info` | name, student ID, class (the app doesn't store ID card number, address, phone, date of birth or email) |
| `v1/semester-year/short` | terms |
| `v1/student/schedule` | timetable |
| `thoi-khoa-bieu/lich-thi-sinh-vien/v1` | exam schedule |
| `share/ket-qua-hoc-tap/...` (POST, sends the student ID) | transcript, curriculum, component scores |
| `v1/student/status-decision/...`, `v1/student-activities/social-workdays/...`, `v1/tuition-fees/...`, `v1/course-register-results/...` | academic decisions, community service days, tuition, registered courses |

- The `share/ket-qua-hoc-tap/...` POST APIs are read queries: MyBK's own **Bảng điểm môn học** (transcript) and **Chương trình đào tạo** (curriculum) pages send exactly these requests when you only open them to look (checked on October 2, 2026). The other APIs come from the MyBK page's source code.
- The course registration page (`/dkmh/dangKyMonHocForm.action`) is opened only to read the table of registration rounds, at most once a day.

## 4. How often

| What | When |
|---|---|
| LMS sync | when the app opens (if more than 3 hours have passed), then every 3 hours; only this term's classes. A sync usually sends about 15–30 requests |
| MyBK sync | every 12 hours; 13 APIs, plus the registration page once a day |
| Keep the SSO session alive | every 60 minutes while the app is open |
| Spacing | LMS API calls 300 ms apart, files 1 second apart, MyBK API calls 300 ms apart |
| Server reports overload (HTTP 429/503) | the app waits per `Retry-After` (or a random, increasing delay if there's none) and tries again; at most 3 attempts in total, then it stops and reports the error |
| Not signed in | nothing is sent |

You can always select **Sync** to run it now; the app skips it if a sync finished less than 2 minutes ago.

## 5. Outside the university

- **GitHub**: only if you allow update checks (see [Updating](Updating)).
- No server of its own, no analytics, no ads.
- University pages you open in the app may load their own resources (fonts, scripts). The app doesn't block them, but it doesn't let the hidden window move to pages outside the university.
