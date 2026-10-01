# Chính sách quyền riêng tư — BK Study Desk

Cập nhật: 01/10/2026

BK Study Desk là app không chính thức, do cá nhân (xeroz369) phát triển cho sinh viên Trường Đại học Bách Khoa – ĐHQG TP.HCM (HCMUT). App không thuộc trường và không được trường bảo trợ.

## App thu thập gì

**Không gì cả, theo nghĩa gửi về cho người làm app.** App không có server riêng, không có analytics, không có quảng cáo, không gửi dữ liệu đi đâu ngoài server của trường.

App chỉ kết nối tới các trang của HCMUT mà bạn vốn dùng:
- `sso.hcmut.edu.vn`: trang đăng nhập;
- `lms.hcmut.edu.vn`: BK-LMS;
- `mybk.hcmut.edu.vn`: MyBK;
- các dịch vụ khác của trường mà bạn tự bấm mở.

## Dữ liệu lưu trên máy bạn

Sau khi bạn đăng nhập, app tải về và lưu **trên máy của bạn**:
- deadline, lịch học, lịch thi;
- tài liệu môn học;
- điểm, tiến độ chương trình đào tạo;
- họ tên, MSSV, lớp (để hiển thị).

Những dữ liệu này nằm trong folder dữ liệu của app:
- bản Microsoft Store: trong `%LOCALAPPDATA%`, Windows tự xóa khi gỡ app;
- bản zip: folder `data\` cạnh app.

Tài liệu môn học lưu ở folder bạn chọn, mặc định `Documents\BK Study Desk`.

App **không lưu** số CCCD, địa chỉ, số điện thoại, ngày sinh hay email cá nhân, kể cả khi MyBK trả về.

## Đăng nhập và bảo mật

- Mật khẩu chỉ được gõ trên trang SSO của trường, trong trình duyệt nhúng (WebView2). App không đọc và không lưu mật khẩu.
- Cookie đăng nhập nằm trong profile WebView2, do WebView2 mã hóa.
- Token BK-LMS được mã hóa bằng Windows DPAPI, chỉ tài khoản Windows của bạn giải mã được.
- App chỉ **đọc** dữ liệu. App không nộp bài, không đăng ký hay hủy môn, không thanh toán, không làm quiz thay bạn.

## Xóa dữ liệu

- Trong app: **Cài đặt → Đăng xuất** để xóa token và cookie đăng nhập.
- Gỡ app: bản Store tự xóa dữ liệu app; bản zip thì xóa folder app. Tài liệu đã tải trong folder bạn chọn vẫn còn, bạn tự xóa nếu muốn.

## Liên hệ

Câu hỏi hoặc báo lỗi: [GitHub Issues](https://github.com/xeroz369/bk-study-desk/issues).

---

# Privacy Policy — BK Study Desk (English)

BK Study Desk is an unofficial app made by an individual developer (xeroz369) for HCMUT students. It is not affiliated with or endorsed by the university.

- **No data collection by the developer.** No server, no analytics, no ads. The app only talks to the university's own sites (SSO, BK-LMS, MyBK).
- **Data stored locally:** after you sign in, deadlines, schedules, course documents, grades, your name, student ID and class are stored on your PC only:
  - in `%LOCALAPPDATA%` for the Store version, removed on uninstall;
  - next to the app for the zip version.
- **Not stored:** ID card number, address, phone number, date of birth or personal email.
- **Sign-in:** your password is typed only on the university SSO page, and the app never reads or stores it. Cookies are encrypted by WebView2, and the LMS token is encrypted with Windows DPAPI.
- **Read-only:** the app never submits, registers, pays or takes quizzes for you.
- **Delete data:** use *Settings → Sign out*, or uninstall the app.
- **Contact:** [GitHub Issues](https://github.com/xeroz369/bk-study-desk/issues).
