# Quy trình kiểm tra bảo mật

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](quy-trinh-bao-mat.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](en/security-process.md)

Tài liệu này mô tả BK Study Desk được kiểm tra bảo mật thế nào, bằng công cụ gì, lúc nào, và kết quả được xử lý ra sao. Báo lỗ hổng: xem [SECURITY.md](../SECURITY.md).

Nguyên tắc: dùng công cụ có sẵn, được duy trì (không tự viết công cụ quét), chạy tự động trên GitHub Actions, kết quả hiện ở tab **Security** > **Code scanning** của repo.

## 1. Cần bảo vệ cái gì

| Tài sản | Ở đâu | Rủi ro nếu lộ hoặc bị sửa |
|---|---|---|
| Phiên đăng nhập HCMUT (cookie SSO, LMS, MyBK) | hồ sơ WebView2 trong `%LOCALAPPDATA%\BKStudyDesk.Data` | người khác đăng nhập thay sinh viên |
| Token LMS | file mã hóa DPAPI, thư mục chỉ tài khoản Windows của người dùng đọc được | đọc dữ liệu LMS thay người dùng |
| Dữ liệu học tập (điểm, lịch, CTĐT, kết quả luyện tập) | `data\*.json` | lộ thông tin cá nhân |
| Gói cập nhật, bộ cài | GitHub Releases, feed `updates` | cài mã độc lên máy người dùng |
| Mã nguồn và CI | repo GitHub, workflow | chèn mã vào bản phát hành |

## 2. Bề mặt tấn công và cách chặn

| Bề mặt | Chặn bằng |
|---|---|
| Khung Luyện tập (WebView2, host ảo `sohoc.example`) | không mở port mạng; `/api` chỉ nhận request có header `X-App` từ host của app; tắt host object, DevTools, thả file; link ngoài mở bằng trình duyệt |
| Tin nhắn từ khung Luyện tập lên app (`postMessage`) | chỉ nhận từ host của app, chỉ các lệnh phím tắt và mở trang |
| Gói quiz từ người khác (Study Pack, Markdown, Moodle XML, GIFT, Aiken) | `validatePack`: chặn script, thuộc tính sự kiện, ảnh ngoài; id gói chỉ gồm ký tự an toàn, không ghi ra ngoài thư mục gói |
| Đường dẫn file do trang gửi lên | `Paths.StudyPath` chỉ cho đường dẫn bên trong thư mục học tập |
| WebView ẩn đọc MyBK | chỉ đi tới tên miền trường qua https; đổi http sang https trong cùng tên miền; chặn cửa sổ mới |
| Thao tác lên LMS/MyBK | chỉ đọc theo hành vi: xem, mở form, tải danh sách; không gửi đăng ký, hủy, lưu, thanh toán |
| Cập nhật app | Velopack kiểm SHA-256; nguồn chỉ là repo GitHub qua https; bộ cài có attestation build từ GitHub Actions |
| CI và chuỗi cung ứng | action ghim theo SHA commit; quyền token mặc định rỗng, cấp riêng từng job; nhánh `main` chỉ nhận PR qua check; job nộp Store phải được duyệt tay |

## 3. Công cụ và khi nào chạy

| Công cụ | Kiểm gì | Mỗi PR | Hằng tuần | File |
|---|---|---|---|---|
| CodeQL | lỗi bảo mật trong mã C#, TypeScript/Svelte, workflow | có | có | `codeql.yml` |
| NuGet advisory (`dotnet list package --vulnerable --include-transitive`) | thư viện .NET có lỗ hổng đã công bố, kể cả thư viện gián tiếp | có | có | `security.yml` (deps) |
| npm audit | thư viện npm có lỗ hổng (mức moderate trở lên thì đỏ) | có | có | `security.yml` (deps) |
| OSV-Scanner (Google) | đối chiếu mọi lockfile với OSV.dev | có | có | `security.yml` (osv) |
| gitleaks | khóa, token, mật khẩu lỡ commit, quét toàn bộ lịch sử | có | có | `security.yml` (secrets) |
| zizmor | lỗi bảo mật trong workflow GitHub Actions | có | có | `security.yml` (zizmor) |
| Semgrep (bộ luật cộng đồng) | mẫu mã nguy hiểm C#, TS/JS, bí mật trong mã | có | có | `security.yml` (semgrep) |
| BinSkim (Microsoft) | file .exe/.dll đã build: ASLR, DEP, CFG, cờ biên dịch an toàn | có | có | `security.yml` (binskim) |
| OpenSSF Scorecard | thực hành an toàn của repo (bảo vệ nhánh, ghim action, quyền token...) | khi vào `main` | có | `scorecard.yml` |
| Dependabot | mở PR nâng thư viện có lỗ hổng hoặc bản mới | | có | `.github/dependabot.yml` |
| Quét dấu vết cá nhân | tên, MSSV, email, đường dẫn máy trong mã và bản build trước khi xuất ra repo công khai | trước mỗi lần xuất | | `tools-dev/leak-scan.ps1` |

Chạy lại bằng tay: tab **Actions** > **security** > **Run workflow**.

## 4. Xử lý kết quả

| Mức | Ví dụ | Thời hạn |
|---|---|---|
| Nghiêm trọng / Cao | lộ token, chạy mã lạ, ghi file ngoài thư mục, gửi request thay đổi dữ liệu trên LMS/MyBK, thư viện có lỗ hổng bị khai thác được | sửa và phát hành bản vá trong 7 ngày, không gộp chờ tính năng |
| Trung bình | lỗ hổng cần điều kiện khó, cấu hình CI chưa chặt | sửa trong 30 ngày, ở bản kế tiếp |
| Thấp / thông tin | khuyến nghị tăng cứng, cảnh báo không áp dụng | ghi vào backlog |

- Mỗi cảnh báo được xem: thật thì sửa và ghi vào CHANGELOG mục **Bảo mật**; không áp dụng thì **Dismiss** trên Code scanning kèm lý do (ví dụ "chỉ chạy trong test", "đầu vào đã được kiểm ở X").
- PR làm check bảo mật đỏ không được merge, trừ khi đã dismiss có lý do.
- Lỗ hổng do người ngoài báo: phản hồi trong 7 ngày (SECURITY.md), sửa riêng qua GitHub Security Advisory, công bố sau khi có bản vá.

## 5. Kiểm tra tay trước mỗi bản phát hành

- [ ] Các workflow `build`, `codeql`, `security` xanh trên commit phát hành.
- [ ] Không còn cảnh báo Cao/Nghiêm trọng mở trên Code scanning và Dependabot.
- [ ] Đổi ở khung Luyện tập, `ApiRouter`, `WebUi`, `MybkRunner`, `Paths`, cập nhật: đọc lại bảng mục 2, ghi lý do trong PR.
- [ ] Thao tác mới với LMS/MyBK: đã đọc mã JS của nút, xác nhận chỉ xem, không gửi dữ liệu.
- [ ] Quét dấu vết cá nhân sạch khi xuất ra repo công khai.
- [ ] Bộ cài có SHA256SUMS và attestation.

## 6. Chưa làm (giới hạn hiện tại)

- Bộ cài chưa có chữ ký số (Windows hiện cảnh báo SmartScreen).
- Chưa có kiểm thử xâm nhập (pentest) thủ công hay fuzz cho `ApiRouter` và các bộ đọc định dạng quiz.
- Repo review (private) không có Code scanning: các check vẫn chạy và báo đỏ, nhưng không có SARIF.
