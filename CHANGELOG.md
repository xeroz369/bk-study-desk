# Nhật ký thay đổi

**Tiếng Việt** · [English](#english)

Ghi các thay đổi đáng chú ý của BK Study Desk. Định dạng theo [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) (tên mục dịch sang tiếng Việt), số phiên bản theo [SemVer 2.0.0](https://semver.org/spec/v2.0.0.html).

## [Chưa phát hành]

### Thay đổi

- Tài liệu trên GitHub (README, PRIVACY, SECURITY, CONTRIBUTING, CHANGELOG, quy tắc ứng xử, studypack) hiện cả hai thứ tiếng trên một trang: phần English ở cuối, thu gọn. Trang **Giới thiệu** mở thẳng phần English khi giao diện tiếng Anh.

## [1.1.3] - 2026-10-03

### Thêm

- **Lịch** → **Thời khóa biểu** có dạng **lưới tuần** giống Google Calendar ([#5](https://github.com/xeroz369/bk-study-desk/issues/5)). Lưới tự canh bố cục: Thứ 7, CN chỉ hiện khi có buổi học; khung giờ theo buổi sớm nhất và muộn nhất; buổi trùng giờ chia đôi cột. Dạng danh sách vẫn còn, chọn ở ô **Kiểu xem**.
- **Xuất lịch (.ics)…**: lưu thời khóa biểu cả kỳ và lịch thi ra file iCalendar để nhập vào Google Calendar, Outlook, Lịch của Windows.
- Đồng bộ LMS/MyBK lỗi thì thanh báo có thêm nút **Mở MyBK** / **Mở LMS** để xem trang đang lỗi gì hoặc đăng nhập lại ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- **Tải tài liệu…** chọn được loại file: PDF, slide (.ppt, .pptx), khác; số file và dung lượng tính theo lựa chọn ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- Bảng **Kết quả đăng ký** có thêm cột giảng viên và giờ học, lấy từ thời khóa biểu MyBK ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- Tài liệu tiếng Anh tách thành file riêng, dịch đầy đủ: phần English ở cuối README, PRIVACY, SECURITY, quy tắc ứng xử, CONTRIBUTING, CHANGELOG và tài liệu studypack, cùng các trang Wiki tiếng Anh. Chuẩn viết tiếng Anh: `docs/english-style.md`.
- Mục **Ủng hộ** trong README và Wiki: Ko-fi và mã QR MoMo/VietQR.

### Thay đổi

- README và Wiki: khuyên dùng bộ cài trên GitHub trước, Microsoft Store sau (bản Store phải chờ Microsoft duyệt nên thường chậm hơn).

### Sửa lỗi

- Thời khóa biểu dạng danh sách có nhóm "Thứ 0" với giờ lạ (ví dụ "0:00–6:50"). Môn không có giờ cố định giờ ghi riêng ở dòng "Không có giờ cố định".

## [1.1.2] - 2026-10-03 [YANKED]

Đã gỡ khỏi trang phát hành vì bản này có mục ủng hộ trong app. Mọi thay đổi chuyển sang 1.1.3.

## [1.1.1] - 2026-10-03

### Thay đổi

- Chữ trên giao diện và tài liệu theo chuẩn văn phong `docs/van-phong.md`: "Sao chép" thay "Copy", "Chọn" thay "Bấm", "tải xuống" thay "tải về"; câu lỗi nói rõ cần làm gì.
- Trang Luyện tập: mục "Các môn" đổi tên thành "Môn học".

### Thêm

- README và Wiki: hướng dẫn cài từ Microsoft Store, lưu ý kiểm tra số phiên bản vì bản Store phải chờ Microsoft duyệt.
- Ảnh minh họa mới (dữ liệu demo) cho README, Wiki và trang Store.
- Email liên hệ chính thức của dự án: bkstudydesk@xerozsoft.com (README, SECURITY, PRIVACY, quy tắc ứng xử).

### Sửa lỗi

- Mốc **quiz mở** trên LMS bị hiện như hạn nộp ("còn 2 ngày" tính tới giờ mở). Giờ mốc này ghi "Quiz mở" kèm hạn đóng, và không còn được tính vào "Quiz trong 14 ngày".
- Đồng bộ MyBK báo "không phản hồi sau 45 giây (đang ở blank)" và lần **Thử lại** nào cũng lỗi y hệt:
  - Lần mở MyBK bị hủy hoặc lỗi thì app báo ngay, kèm lý do.
  - Sau lỗi, app bỏ WebView ẩn cũ, lần thử lại dùng WebView mới.
  - Log ghi rõ khi WebView2 bị lỗi hoặc khi MyBK chuyển sang trang ngoài trường.
- Bảng trống (Sổ điểm LMS, Bảng điểm, Chương trình đào tạo, đăng ký, công tác xã hội, quyết định học vụ) hiện các cột co lại, không có chữ. Giờ bảng trống ghi rõ lý do: đang đồng bộ, đồng bộ lỗi, hoặc thật sự không có gì.
- Bấm chuột vào tab hoặc mục trên thanh điều hướng thì hiện khung trắng quanh mục đó. Giờ khung focus chỉ hiện khi dùng bàn phím.

## [1.1.0] - 2026-10-03

### Thêm

- Có trên Microsoft Store: cài không bị cảnh báo SmartScreen, Store tự cập nhật.
- Thanh trạng thái hiện bước đang làm khi đồng bộ (ví dụ "LMS: đang đọc quiz…", "đang kiểm tra lớp… (3/5)").
- InfoBar báo lỗi theo chuẩn Windows: biểu tượng theo mức độ, câu dễ hiểu kèm cách xử lý, mục **Chi tiết** chứa chữ gốc của máy chủ (có nút **Sao chép**). Thanh trạng thái có biểu tượng lỗi cạnh LMS/MyBK.
- Cửa sổ LMS/MyBK báo khi trang tải quá 10 giây, mất mạng, máy chủ lỗi hoặc phiên đăng nhập đã hết, kèm nút **Thử lại**.
- Giữ phiên đăng nhập khi app đang mở: mỗi 60 phút ghé SSO một lần (tắt được trong Cài đặt). Phiên SSO của trường vẫn hết sau tối đa 8 giờ kể từ lúc đăng nhập.
- **Cài đặt** → **Nhật ký hoạt động**: nút mở file log; bật ghi log chẩn đoán khi cần báo lỗi (tự tắt sau 7 ngày).

### Thay đổi

- Mặc định chỉ đọc môn của học kỳ này và không tự tải tài liệu. Muốn tải thì vào **Môn học** → **Tải tài liệu…** và chọn mục cần tải; bật lại tự tải trong Cài đặt nếu muốn.
- Lần đầu đăng nhập, lịch, hạn nộp, quiz và điểm hiện ngay, không chờ tải tài liệu.
- Đọc ít request hơn: thông báo diễn đàn chỉ lấy lớp học kỳ này; quiz không có hạn đóng chỉ kiểm tra ngày một lần; trang Môn học chỉ quét thư mục khi đang mở.
- Giãn cách request tới LMS và MyBK. Máy chủ báo quá tải (HTTP 429/503) thì app chờ theo `Retry-After` rồi mới thử lại.
- **Giữ đăng nhập khi tắt rồi mở lại app** giờ giữ tối đa 8 giờ, bằng giới hạn phiên của máy chủ trường (trước đây ghi 30 ngày nhưng máy chủ đã hủy phiên từ lâu).
- `data\config.json` chỉ lưu những gì bạn đổi so với mặc định.
- Trang trống vì chưa có dữ liệu thì ghi rõ đang tải lần đầu, bị lỗi hay chưa đăng nhập.

### Sửa lỗi

- Lịch LMS không đọc được (app xin quá 50 mục một lần; giờ đọc theo trang).
- Lỗi đọc từng phần (sổ điểm một môn, một API MyBK, tải file) trước đây bị bỏ qua im lặng; giờ hiện trên thanh báo và ghi log.
- Đồng bộ báo "Access denied" khi giao diện đang đọc dữ liệu đúng lúc đồng bộ ghi lại.
- Cuộn chuột bị kẹt khi đi qua ô nhập hoặc bảng; tiêu đề cột "Điểm chữ", "Thang điểm" bị cắt; ngày và số ngày công tác xã hội sai định dạng.
- Cửa sổ chính và cửa sổ đăng nhập tràn ra ngoài màn hình nhỏ.
- Nút **Cài đặt** và **Giới thiệu** không phản hồi khi dùng trình đọc màn hình.

### Bỏ

- Tham số `downloads.watch` trong config (không có tác dụng).

### Bảo mật

- Nguồn cập nhật chỉ nhận repo GitHub qua https; thư mục trên máy chỉ dùng cho bản cài thử.
- Log che token, cookie, ticket đăng nhập CAS và email trước khi ghi.
- Tắt tự điền dữ liệu chung của WebView2. Mật khẩu chỉ được lưu khi bạn chọn **Lưu** lúc trình duyệt trong app hỏi.

## [1.0.6] - 2026-10-02

### Thêm

- Bộ cài tiếng Việt: chọn thư mục cài, không cần quyền admin. Bộ gỡ hỏi giữ hay xóa dữ liệu app.
- Tự cập nhật theo lựa chọn của bạn: báo khi có bản mới, tự cài khi tắt app hoặc không kiểm tra.
- Luyện tập: soạn quiz, nhờ AI soạn, nhập/xuất Markdown, ôn quiz LMS đã nộp, thi thử.
- Lấy dữ liệu từ bản zip cũ trong Cài đặt.

### Thay đổi

- Dữ liệu app ở `%LOCALAPPDATA%\BKStudyDesk.Data`.
- Giấy phép đổi sang PolyForm Noncommercial 1.0.0 (không dùng cho mục đích thương mại). Các bản trước vẫn theo MIT.

### Sửa lỗi

- Mở LMS/MyBK khi phiên hết hạn hiện "session timed out".

### Bảo mật

- Vá một số lỗi bảo mật nhỏ (giới hạn điều hướng của WebView ẩn, không chạy file tải xuống, giới hạn kích thước file).

Các bản trước: xem [Releases](https://github.com/xeroz369/bk-study-desk/releases).

[Chưa phát hành]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.3...HEAD
[1.1.3]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.1...v1.1.3
[1.1.2]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/xeroz369/bk-study-desk/compare/v1.0.6...v1.1.0
[1.0.6]: https://github.com/xeroz369/bk-study-desk/releases/tag/v1.0.6

---

## English

<details>
<summary><b>Changelog</b> (English version)</summary>

Notable changes to BK Study Desk. The format follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and versions follow [SemVer 2.0.0](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Documents on GitHub (README, PRIVACY, SECURITY, CONTRIBUTING, CHANGELOG, code of conduct, studypack) show both languages on one page, with the English part collapsed at the end. The **About** page jumps straight to the English part when the interface is in English.

## [1.1.3] - 2026-10-03

### Added

- **Calendar** > **Timetable** has a **week grid** view like Google Calendar ([#5](https://github.com/xeroz369/bk-study-desk/issues/5)). The grid lays itself out: Saturday and Sunday appear only when there are classes, the hours span the earliest to the latest class, and overlapping classes share the column. The list view is still there; pick it under **View**.
- **Export calendar (.ics)**: saves the whole-term timetable and exams as an iCalendar file to import into Google Calendar, Outlook or Windows Calendar.
- When an LMS or MyBK sync fails, the error bar has an **Open MyBK** / **Open LMS** button to see what the page shows or to sign in again ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- **Download** lets you choose file types: PDF, slides (.ppt, .pptx), other; the file count and size follow your choice ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- The course registration table shows the lecturer and class times, taken from the MyBK timetable ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- English documents are separate, complete translations: an English section at the end of the README, PRIVACY, SECURITY, code of conduct, CONTRIBUTING, CHANGELOG and studypack documents, plus English Wiki pages. English style guide: `docs/english-style.md`.
- A **Support** section in the README and the Wiki: Ko-fi and a MoMo/VietQR code.
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
- The status bar shows the current sync step (for example "LMS: reading quizzes…", "checking classes… (3/5)").
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

[Unreleased]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.3...HEAD
[1.1.3]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.1...v1.1.3
[1.1.2]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/xeroz369/bk-study-desk/compare/v1.0.6...v1.1.0
[1.0.6]: https://github.com/xeroz369/bk-study-desk/releases/tag/v1.0.6

</details>
