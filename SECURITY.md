# Chính sách bảo mật

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](SECURITY.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](docs/en/SECURITY.md)

## Báo lỗ hổng

**Đừng báo lỗ hổng bảo mật ở issue công khai.** Hãy báo riêng qua [**Security** > **Report a vulnerability**](https://github.com/xeroz369/bk-study-desk/security/advisories/new), hoặc gửi email tới [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).

Nên có:
- phiên bản app, Windows;
- các bước repeat lỗi;
- ảnh hưởng: lộ token/phiên đăng nhập, chạy mã lạ, ghi file ngoài thư mục app, gửi request làm thay đổi dữ liệu trên LMS/MyBK...

Mình sẽ phản hồi trong vòng 7 ngày, sửa ở bản kế tiếp và ghi công người báo (nếu bạn muốn).

## Phiên bản được hỗ trợ

Chỉ bản mới nhất ở trang [Releases](https://github.com/xeroz369/bk-study-desk/releases) được sửa lỗi bảo mật. Bật cập nhật trong **Cài đặt** > **Cập nhật** để luôn ở bản mới.

## App bảo vệ bạn thế nào

- Mật khẩu chỉ gõ trên trang SSO của trường; app không đọc. Chọn **Lưu** khi được hỏi thì WebView2 lưu mã hóa trên máy để tự điền. Ngoại lệ duy nhất: nếu bạn tự bật **Tự đăng nhập lại khi hết phiên** (mặc định tắt), bạn nhập tài khoản vào app; app lưu trong Windows Credential Manager (không nằm trong thư mục dữ liệu) và chỉ điền vào trang SSO của trường. Tắt tính năng hay đăng xuất thì xóa.
- Token LMS mã hóa bằng Windows DPAPI; dữ liệu nằm trong `%LOCALAPPDATA%`, chỉ tài khoản Windows của bạn (cùng SYSTEM và Administrators) mở được.
- App chỉ gọi API **đọc**; không nộp bài, đăng ký, hủy hay thanh toán.
- Gói cập nhật được kiểm SHA-256 trước khi cài; nguồn cập nhật chỉ nhận repo GitHub qua https (thư mục trên máy chỉ dùng ở bản cài thử).
- Gói quiz nhận từ người khác được kiểm tra và lọc HTML; không chạy script, không tải ảnh ngoài.
- Mỗi thay đổi đều qua kiểm tra bảo mật tự động: CodeQL, Semgrep, OSV-Scanner, npm audit, NuGet advisory, gitleaks, zizmor, BinSkim, OpenSSF Scorecard; thư viện được Dependabot theo dõi. Chi tiết và cách xử lý kết quả: [Quy trình kiểm tra bảo mật](docs/quy-trinh-bao-mat.md).
