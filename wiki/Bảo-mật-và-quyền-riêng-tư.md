**Tiếng Việt** | [English](Security-and-privacy)

- Mật khẩu chỉ gõ trên trang SSO của trường; app không đọc. Chọn **Lưu** khi được hỏi thì WebView2 lưu mã hóa trên máy để tự điền lần sau.
- Token LMS mã hóa bằng Windows DPAPI; cookie do WebView2 mã hóa. Dữ liệu nằm trong `%LOCALAPPDATA%`, chỉ tài khoản Windows của bạn (cùng SYSTEM và Administrators) mở được.
- App chỉ **đọc**: không nộp bài, đăng ký, hủy hay thanh toán. Gọi server trường ít nhất có thể (LMS mỗi 3 giờ, khoảng 15–30 request; MyBK 12 giờ một lần). Danh sách đầy đủ địa chỉ và API: [[Cách app hoạt động]].
- Không lưu CCCD, địa chỉ, số điện thoại, ngày sinh, email cá nhân.
- Không server riêng, không analytics, không quảng cáo. GitHub chỉ được hỏi phiên bản mới khi bạn cho phép.
- Gói cập nhật kiểm SHA-256; quiz nhận từ người khác được lọc HTML, không chạy script.

Chi tiết: [PRIVACY.md](https://github.com/bk-study-desk/bk-study-desk/blob/main/PRIVACY.md) | Báo lỗ hổng: [SECURITY.md](https://github.com/bk-study-desk/bk-study-desk/blob/main/SECURITY.md).