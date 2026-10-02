# Bảo mật và quyền riêng tư

- Mật khẩu chỉ gõ trên trang SSO của trường; app không đọc, không lưu.
- Token LMS mã hóa bằng Windows DPAPI; cookie do WebView2 mã hóa. Dữ liệu nằm trong `%LOCALAPPDATA%`, chỉ tài khoản Windows của bạn mở được.
- App chỉ **đọc**: không nộp bài, đăng ký, hủy hay thanh toán. Gọi server trường ít nhất có thể (LMS khoảng 20 request mỗi 3 giờ, MyBK 12 giờ một lần).
- Không lưu CCCD, địa chỉ, số điện thoại, ngày sinh, email cá nhân.
- Không server riêng, không analytics, không quảng cáo. GitHub chỉ được hỏi phiên bản mới khi bạn cho phép.
- Gói cập nhật kiểm SHA-256; quiz nhận từ người khác được lọc HTML, không chạy script.

Chi tiết: [PRIVACY.md](https://github.com/xeroz369/bk-study-desk/blob/main/PRIVACY.md) · Báo lỗ hổng: [SECURITY.md](https://github.com/xeroz369/bk-study-desk/blob/main/SECURITY.md).