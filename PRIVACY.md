# Chính sách quyền riêng tư của BK Study Desk

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

# BK Study Desk Privacy Policy (English)

BK Study Desk is an unofficial app made by an individual developer (xeroz369) for HCMUT students. It is not affiliated with or endorsed by the university.

- **No data collection by the developer.** No server, no analytics, no ads. The app only talks to the university's own sites (SSO, BK-LMS, MyBK), plus GitHub to check for updates **only if you allow it** (no device or account info is sent).
- **Data stored locally:** after you sign in, deadlines, schedules, course documents, grades, your name, student ID and class, reviews of LMS quizzes you have submitted (can be turned off), and your own quizzes and practice results are stored on your PC only, in `%LOCALAPPDATA%\BKStudyDesk.Data`. The uninstaller asks whether to delete it.
- **Not stored:** ID card number, address, phone number, date of birth or personal email.
- **Sign-in:** your password is typed only on the university SSO page and the app never reads it. If you choose Save when the sign-in window asks, WebView2 stores it encrypted on this PC for autofill. Cookies are encrypted by WebView2, and the LMS token is encrypted with Windows DPAPI.
- **Read-only:** the app never submits, registers, pays or takes quizzes for you.
- **Log:** activity log in `data\app.log` on your PC (about 4 MB max, rotated) for bug reports; tokens, cookies, sign-in tickets and emails are masked.
- **Retention:** data stays on your PC until you delete it. Newer syncs overwrite older data.
- **Roles and law:** applicable law is Vietnam's Personal Data Protection Law No. 91/2025/QH15 and Decree 356/2025/ND-CP (in force since 1 Jan 2026). HCMUT holds the source data; you use the app to view your own data on your own PC; the developer receives no data and, in the author's reading (not confirmed by any authority), is neither a controller nor a processor under Article 2.
- **Your rights:** view everything in the app or the JSON files in `%LOCALAPPDATA%\BKStudyDesk.Data`; correct source data with the university; delete as below.
- **Other people's data:** downloaded data may include lecturers' names or classmates' forum posts. Use it only for your studies; do not republish or share it.
- **Delete data:** *Settings → Sign out* deletes tokens and cookies (synced data is kept for offline viewing). To delete everything, quit the app and delete `%LOCALAPPDATA%\BKStudyDesk.Data`, or uninstall and tick *Delete app data*. The uninstaller never deletes downloaded course documents.
- **Update checks:** GitHub (US) sees your IP address when the app checks for updates, under GitHub's own privacy statement.
- **Contact:** [GitHub Issues](https://github.com/xeroz369/bk-study-desk/issues) (public: do not post personal data). Security issues: [SECURITY.md](SECURITY.md). Private questions: [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).
