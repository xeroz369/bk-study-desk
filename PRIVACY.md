# Chính sách quyền riêng tư của BK Study Desk

Cập nhật: 02/10/2026

BK Study Desk là app không chính thức, do cá nhân (xeroz369) phát triển cho sinh viên Trường Đại học Bách Khoa – ĐHQG TP.HCM (HCMUT). App không thuộc trường và không được trường bảo trợ.

## App thu thập gì

**App không gửi dữ liệu nào về cho người làm app.** App không có server riêng, không có analytics, không có quảng cáo. Ngoài server của trường, app chỉ hỏi GitHub khi bạn cho phép kiểm tra bản mới (xem bên dưới).

App chỉ kết nối tới các trang của HCMUT mà bạn vốn dùng:
- `sso.hcmut.edu.vn`: trang đăng nhập;
- `lms.hcmut.edu.vn`: BK-LMS;
- `mybk.hcmut.edu.vn`: MyBK;
- các dịch vụ khác của trường mà bạn tự bấm mở;
- `github.com` / `api.github.com`: **chỉ khi bạn cho phép** kiểm tra bản mới (Cài đặt → Cập nhật). App chỉ hỏi phiên bản mới nhất và tải gói cập nhật, không gửi thông tin máy hay tài khoản. Chọn "Không kiểm tra" thì app không gọi tới GitHub.

## Dữ liệu lưu trên máy bạn

Sau khi bạn đăng nhập, app tải về và lưu **trên máy của bạn**:
- deadline, lịch học, lịch thi;
- tài liệu môn học;
- điểm, tiến độ chương trình đào tạo;
- họ tên, MSSV, lớp (để hiển thị);
- bản xem lại các quiz LMS **bạn đã nộp** (để ôn lại, tắt được trong Cài đặt; không đọc quiz đang làm);
- quiz bạn tự soạn hoặc nhập, kết quả luyện tập, ghi chú.

Những dữ liệu này nằm trong `%LOCALAPPDATA%\BKStudyDesk.Data`, dù bạn cài app vào thư mục nào. Lúc gỡ app bạn chọn xóa hay giữ.

Tài liệu môn học lưu ở thư mục bạn chọn, mặc định `Documents\BK Study Desk`.

App **không lưu** số CCCD, địa chỉ, số điện thoại, ngày sinh hay email cá nhân, kể cả khi MyBK trả về.

## Đăng nhập và bảo mật

- Mật khẩu chỉ được gõ trên trang SSO của trường, trong trình duyệt nhúng (WebView2). App không đọc và không lưu mật khẩu.
- Cookie đăng nhập nằm trong profile WebView2, do WebView2 mã hóa.
- Token BK-LMS được mã hóa bằng Windows DPAPI, chỉ tài khoản Windows của bạn giải mã được.
- App chỉ **đọc** dữ liệu. App không nộp bài, không đăng ký hay hủy môn, không thanh toán, không làm quiz thay bạn.

## Xóa dữ liệu

- Trong app: **Cài đặt → Đăng xuất** để xóa token và cookie đăng nhập.
- Gỡ app: Cài đặt Windows → Ứng dụng → BK Study Desk → Gỡ cài đặt, tích "Xóa cả dữ liệu của app". Tài liệu đã tải trong thư mục bạn chọn vẫn còn, bạn tự xóa nếu muốn.

## Liên hệ

Câu hỏi hoặc báo lỗi: [GitHub Issues](https://github.com/xeroz369/bk-study-desk/issues).

---

# BK Study Desk Privacy Policy (English)

BK Study Desk is an unofficial app made by an individual developer (xeroz369) for HCMUT students. It is not affiliated with or endorsed by the university.

- **No data collection by the developer.** No server, no analytics, no ads. The app only talks to the university's own sites (SSO, BK-LMS, MyBK), plus GitHub to check for updates **only if you allow it** (no device or account info is sent).
- **Data stored locally:** after you sign in, deadlines, schedules, course documents, grades, your name, student ID and class, reviews of LMS quizzes you have submitted (can be turned off), and your own quizzes and practice results are stored on your PC only, in `%LOCALAPPDATA%\BKStudyDesk.Data`. The uninstaller asks whether to delete it.
- **Not stored:** ID card number, address, phone number, date of birth or personal email.
- **Sign-in:** your password is typed only on the university SSO page, and the app never reads or stores it. Cookies are encrypted by WebView2, and the LMS token is encrypted with Windows DPAPI.
- **Read-only:** the app never submits, registers, pays or takes quizzes for you.
- **Delete data:** use *Settings → Sign out*, or uninstall the app and tick *Delete app data*. Downloaded course documents are never deleted.
- **Contact:** [GitHub Issues](https://github.com/xeroz369/bk-study-desk/issues).
