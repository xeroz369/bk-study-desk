# Chính sách quyền riêng tư của BK Study Desk

**Tiếng Việt** · [English](#english)

Cập nhật: 03/10/2026

BK Study Desk là app không chính thức, do cá nhân (xeroz369) phát triển cho sinh viên Trường Đại học Bách Khoa – ĐHQG TP.HCM (HCMUT). App không thuộc trường, không được trường hay Moodle HQ xác nhận. Mọi địa chỉ và API app gọi được liệt kê ở Wiki [Cách app hoạt động](https://github.com/xeroz369/bk-study-desk/wiki/C%C3%A1ch-app-ho%E1%BA%A1t-%C4%91%E1%BB%99ng).

## App thu thập gì

**App không gửi dữ liệu nào về cho người làm app.** App không có server riêng, không có analytics, không có quảng cáo. Ngoài server của trường, app chỉ hỏi GitHub khi bạn cho phép kiểm tra bản mới (xem bên dưới).

App chỉ kết nối tới các trang của HCMUT mà bạn vốn dùng:
- `sso.hcmut.edu.vn`: trang đăng nhập;
- `lms.hcmut.edu.vn`: BK-LMS;
- `mybk.hcmut.edu.vn`: MyBK;
- các dịch vụ khác của trường mà bạn tự mở;
- `github.com` / `api.github.com`: **chỉ khi bạn cho phép** kiểm tra bản mới (**Cài đặt** → **Cập nhật**; mặc định chưa kiểm tra cho tới khi bạn chọn). App chỉ hỏi phiên bản mới nhất và tải gói cập nhật, không gửi thông tin tài khoản. Như mọi kết nối mạng, GitHub (máy chủ ở Mỹ) thấy địa chỉ IP của bạn, theo [chính sách của GitHub](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement). Chọn **Không kiểm tra** thì app không gọi tới GitHub.

## Dữ liệu lưu trên máy bạn

Sau khi bạn đăng nhập, app tải xuống và lưu **trên máy của bạn**:
- deadline, lịch học, lịch thi;
- tài liệu môn học bạn chọn tải (mặc định app không tự tải);
- điểm, tiến độ chương trình đào tạo;
- họ tên, MSSV, lớp (để hiển thị);
- bản xem lại quiz LMS **bạn đã nộp** (để ôn lại, tắt được trong Cài đặt; không đọc quiz đang làm);
- quiz bạn tự soạn hoặc nhập, kết quả luyện tập, ghi chú.

Những dữ liệu này nằm trong `%LOCALAPPDATA%\BKStudyDesk.Data`, dù bạn cài app vào thư mục nào. Lúc gỡ app bạn chọn xóa hay giữ.

Tài liệu môn học lưu ở thư mục bạn chọn, mặc định `Documents\BK Study Desk`.

App **không lưu** số CCCD, địa chỉ, số điện thoại, ngày sinh hay email cá nhân, kể cả khi MyBK trả về.

App ghi nhật ký hoạt động vào `data\app.log` trên máy (tối đa khoảng 4 MB, tự xoay vòng) để bạn gửi kèm khi báo lỗi. Token, cookie, ticket đăng nhập và email được che trước khi ghi; log có thể có tên môn, tên file, giờ đồng bộ. Bạn mở file này được ở **Cài đặt** → **Nhật ký hoạt động**.

**Thời gian lưu:** dữ liệu trên nằm trên máy tới khi bạn xóa (xem mục Xóa dữ liệu). Bản mới đồng bộ ghi đè bản cũ; app không gửi bản sao đi đâu.

## Vai trò và cơ sở pháp lý

- Văn bản áp dụng: [Luật Bảo vệ dữ liệu cá nhân số 91/2025/QH15](https://congbao.chinhphu.vn/van-ban/luat-so-91-2025-qh15-45578/57730.htm) và [Nghị định 356/2025/NĐ-CP](https://datafiles.chinhphu.vn/cpp/files/vbpq/2026/01/356-nd.signed.pdf) (cùng hiệu lực từ 01/01/2026).
- **Trường Đại học Bách khoa** là nơi nắm dữ liệu gốc (điểm, lịch, hồ sơ trên LMS và MyBK). Muốn sửa hay xóa dữ liệu đó thì liên hệ trường.
- **Bạn** tự dùng app để xem dữ liệu của chính mình, trên máy của mình.
- **Người phát triển** không nhận, không lưu, không truy cập được dữ liệu của bạn. Theo cách hiểu của tác giả, người phát triển không phải bên kiểm soát hay bên xử lý dữ liệu theo Điều 2 Luật 91/2025. Đây là cách hiểu của tác giả, chưa có xác nhận của cơ quan quản lý.

## Quyền của bạn

Theo Điều 4 Luật 91/2025, bạn có quyền biết, xem, sửa, xóa dữ liệu của mình. Với dữ liệu app lưu trên máy:
- **Xem:** mọi thứ app có đều hiện trong app; file nằm ở `%LOCALAPPDATA%\BKStudyDesk.Data` (JSON, đọc được bằng trình soạn thảo).
- **Sửa:** dữ liệu lấy từ trường thì sửa ở trường; app đồng bộ lại theo dữ liệu mới.
- **Xóa:** xem mục Xóa dữ liệu.

## Dữ liệu của người khác

Dữ liệu tải xuống có thể có thông tin của người khác (tên giảng viên, bài đăng của bạn học trên diễn đàn LMS). Chỉ dùng cho việc học của bạn; không đăng lại, chia sẻ hay đưa vào quiz chia sẻ công khai (Điều 4 và Điều 7 Luật 91/2025). Quiz LMS đã lưu chỉ chia sẻ được sau khi quiz đóng và không kèm tên, MSSV.

## Đăng nhập và bảo mật

- Mật khẩu chỉ được gõ trên trang SSO của trường, trong trình duyệt trong app (WebView2). App không đọc mật khẩu. Nếu bạn chọn **Lưu** khi cửa sổ đăng nhập hỏi, WebView2 lưu mật khẩu (mã hóa trên máy) để tự điền lần sau.
- Cookie đăng nhập nằm trong profile WebView2, do WebView2 mã hóa.
- Token BK-LMS được mã hóa bằng Windows DPAPI, chỉ tài khoản Windows của bạn giải mã được.
- App chỉ **đọc** dữ liệu. App không nộp bài, không đăng ký hay hủy môn, không thanh toán, không làm quiz thay bạn.

## Xóa dữ liệu

- Trong app: **Cài đặt** → **Đăng xuất** xóa token và cookie đăng nhập. Dữ liệu đã đồng bộ (lịch, điểm…) vẫn giữ để xem khi không có mạng.
- Xóa hết dữ liệu app: thoát app rồi xóa thư mục `%LOCALAPPDATA%\BKStudyDesk.Data`, hoặc gỡ app như dưới.
- Gỡ app: Cài đặt Windows → Ứng dụng → BK Study Desk → Gỡ cài đặt, chọn ô **Xóa cả dữ liệu của app**. Tài liệu đã tải trong thư mục bạn chọn vẫn còn, bạn tự xóa nếu muốn.

## Liên hệ

Câu hỏi hoặc báo lỗi: [GitHub Issues](https://github.com/xeroz369/bk-study-desk/issues). Issue là công khai: đừng dán tên, MSSV, điểm, token hay ảnh chụp có thông tin cá nhân. Vấn đề bảo mật thì báo riêng theo [SECURITY.md](SECURITY.md). Câu hỏi về quyền riêng tư cần trao đổi riêng: [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).

---

## English

<details>
<summary><b>BK Study Desk Privacy Policy</b> (English version)</summary>

Updated: 3 October 2026

BK Study Desk is an unofficial app developed by an individual (xeroz369) for students of Ho Chi Minh City University of Technology – VNU-HCM (HCMUT). It is not part of the university and is not endorsed by the university or Moodle HQ. Every address and API the app calls is listed on the Wiki page [How the app works](https://github.com/xeroz369/bk-study-desk/wiki/C%C3%A1ch-app-ho%E1%BA%A1t-%C4%91%E1%BB%99ng) (Vietnamese).

## What the app collects

**The app sends no data to the developer.** It has no server of its own, no analytics and no ads. Apart from the university's servers, it only contacts GitHub if you allow update checks (see below).

The app connects only to the HCMUT sites you already use:
- `sso.hcmut.edu.vn`: sign-in page;
- `lms.hcmut.edu.vn`: BK-LMS;
- `mybk.hcmut.edu.vn`: MyBK;
- other university services you open yourself;
- `github.com` / `api.github.com`: **only if you allow** update checks (**Settings** > **Updates**; nothing is checked until you choose). The app only asks for the latest version and downloads the update package; it sends no account information. As with any network connection, GitHub (servers in the US) sees your IP address, under [GitHub's privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement). Choose **Don't check** and the app does not contact GitHub.

## Data stored on your PC

After you sign in, the app downloads and stores **on your PC**:
- deadlines, class timetable, exam schedule;
- course documents you choose to download (the app does not download them automatically by default);
- grades and curriculum progress;
- your name, student ID and class (for display);
- reviews of LMS quizzes **you have submitted** (for revision; can be turned off in Settings; quizzes in progress are never read);
- quizzes you write or import, practice results, notes.

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

- In the app: **Settings** > **Sign out** deletes the token and sign-in cookies. Synced data (timetable, grades…) is kept for offline viewing.
- Delete all app data: quit the app, then delete the folder `%LOCALAPPDATA%\BKStudyDesk.Data`, or uninstall as below.
- Uninstall: Windows **Settings** > **Apps** > **BK Study Desk** > **Uninstall**, and select **Xóa cả dữ liệu của app** (Delete app data) in the uninstaller. Documents downloaded to your chosen folder are kept; delete them yourself if you wish.

## Contact

Questions or bug reports: [GitHub Issues](https://github.com/xeroz369/bk-study-desk/issues). Issues are public: do not post names, student IDs, grades, tokens or screenshots with personal information. Report security issues privately per [SECURITY.md](SECURITY.md#english). Private privacy questions: [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).

</details>
