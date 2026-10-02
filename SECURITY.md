# Chính sách bảo mật

**Tiếng Việt** · [English](#english)

## Báo lỗ hổng

**Đừng báo lỗ hổng bảo mật ở issue công khai.** Hãy báo riêng qua [Security → Report a vulnerability](https://github.com/xeroz369/bk-study-desk/security/advisories/new), hoặc gửi email tới [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).

Nên có:
- phiên bản app, Windows;
- các bước repeat lỗi;
- ảnh hưởng: lộ token/phiên đăng nhập, chạy mã lạ, ghi file ngoài thư mục app, gửi request làm thay đổi dữ liệu trên LMS/MyBK…

Mình sẽ phản hồi trong vòng 7 ngày, sửa ở bản kế tiếp và ghi công người báo (nếu bạn muốn).

## Phiên bản được hỗ trợ

Chỉ bản mới nhất ở trang [Releases](https://github.com/xeroz369/bk-study-desk/releases) được sửa lỗi bảo mật. Bật cập nhật trong **Cài đặt** → **Cập nhật** để luôn ở bản mới.

## App bảo vệ bạn thế nào

- Mật khẩu chỉ gõ trên trang SSO của trường; app không đọc. Chọn **Lưu** khi được hỏi thì WebView2 lưu mã hóa trên máy để tự điền.
- Token LMS mã hóa bằng Windows DPAPI; dữ liệu nằm trong `%LOCALAPPDATA%`, chỉ tài khoản Windows của bạn (cùng SYSTEM và Administrators) mở được.
- App chỉ gọi API **đọc**; không nộp bài, đăng ký, hủy hay thanh toán.
- Gói cập nhật được kiểm SHA-256 trước khi cài; nguồn cập nhật chỉ nhận repo GitHub qua https (thư mục trên máy chỉ dùng ở bản cài thử).
- Gói quiz nhận từ người khác được kiểm tra và lọc HTML; không chạy script, không tải ảnh ngoài.
- Mã nguồn được quét tự động bằng CodeQL; thư viện được theo dõi lỗ hổng bằng Dependabot.

---

## English

<details>
<summary><b>Security policy</b> (English version)</summary>

## Reporting a vulnerability

**Do not report security vulnerabilities in public issues.** Report them privately via [**Security** > **Report a vulnerability**](https://github.com/xeroz369/bk-study-desk/security/advisories/new), or email [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).

Please include:
- the app version and Windows version;
- steps to reproduce;
- the impact: leaked token or session, running untrusted code, writing files outside the app folder, sending requests that change data on LMS/MyBK…

You will get a reply within 7 days. The fix ships in the next release, with credit to the reporter if you wish.

## Supported versions

Only the latest version on [Releases](https://github.com/xeroz369/bk-study-desk/releases) receives security fixes. Turn on updates in **Settings** > **Updates** to stay current.

## How the app protects you

- Your password is typed only on the university SSO page; the app never reads it. If you choose **Save** when asked, WebView2 stores it encrypted on your PC for autofill.
- The LMS token is encrypted with Windows DPAPI; data lives in `%LOCALAPPDATA%`, readable only by your Windows account (plus SYSTEM and Administrators).
- The app only calls **read** APIs; it never submits, registers, cancels or pays.
- Update packages are verified with SHA-256 before installing; the update source must be the GitHub repository over https (a local folder is accepted only in test installs).
- Quiz packs from other people are validated and their HTML is sanitized; no scripts run and no external images load.
- The source is scanned automatically with CodeQL; dependencies are monitored for vulnerabilities by Dependabot.

</details>
