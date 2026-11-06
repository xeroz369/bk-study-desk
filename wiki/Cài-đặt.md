**Tiếng Việt** | [English](Installation)

**Yêu cầu:** Windows 10 1809 trở lên hoặc Windows 11, x64 hoặc ARM64. Không cần cài .NET. Cần Microsoft Edge WebView2 Runtime (Windows 11 có sẵn).

## Bộ cài trên GitHub

Bản mới có ở đây ngay khi phát hành, và app tự báo khi có bản sau.

1. Vào [Releases](https://github.com/xeroz369/bk-study-desk/releases/latest), tải `BKStudyDesk-x.y.z-Setup-x64.exe`. Máy chip ARM (Surface Pro X, Snapdragon) tải bản `-Setup-arm64.exe`.
2. Mở file. Nếu Windows hiện **"Windows đã bảo vệ máy tính của bạn"**: chọn **Thông tin thêm** > **Vẫn chạy**. Cảnh báo này có vì bộ cài chưa có chữ ký số; chỉ hiện một lần lúc cài.
3. Chọn thư mục cài (mặc định trong tài khoản của bạn; đổi sang ổ D: được), chọn **Cài đặt**. Không cần quyền admin.
4. Cài xong app tự mở. Lối tắt có ở Desktop và Start menu.

![Bộ cài](images/bo-cai.png)

## Microsoft Store

Bản trên Microsoft Store không còn được cập nhật. Nếu đã cài từ Store, hãy gỡ bản đó rồi cài bộ cài trên GitHub.

## Lần mở đầu

- Chọn **thư mục lưu tài liệu môn học** (gợi ý ổ khác ổ C nếu có).
- Chọn cách **cập nhật**, xem [[Cập nhật]].
- Chọn **Đăng nhập HCMUT**. Một lần đăng nhập dùng cho cả LMS và MyBK.

## Đang dùng bản zip cũ?

Cài bản mới, rồi vào **Cài đặt** > **Cập nhật** > **Lấy dữ liệu từ bản zip**, chọn thư mục bản zip. App chép đăng nhập LMS, kết quả luyện tập, quiz và cài đặt sang, rồi khởi động lại. Sau đó đăng nhập HCMUT lại một lần. Xong thì xóa thư mục bản zip được.

## Kiểm tra file tải xuống (không bắt buộc)

Mở PowerShell ở thư mục chứa file:
```powershell
(Get-FileHash .\BKStudyDesk-x.y.z-Setup-x64.exe -Algorithm SHA256).Hash
```
So với dòng tương ứng trong `SHA256SUMS.txt` ở trang Releases.

Kiểm kỹ hơn (cần cài [GitHub CLI](https://cli.github.com)): xác nhận bộ cài được build từ đúng mã nguồn trên GitHub, không ai sửa giữa chừng:
```powershell
gh attestation verify .\BKStudyDesk-x.y.z-Setup-x64.exe --repo xeroz369/bk-study-desk
```
## macOS và Linux (thử nghiệm)

Bản cho macOS và Linux **chưa ổn định**. Từ 1.2.2 có bộ cài ở [Releases](https://github.com/xeroz369/bk-study-desk/releases/latest): AppImage cho Linux, `.pkg` cho macOS (chưa ký số). Cách cài và chạy từ mã nguồn: xem mục [Bản cho macOS và Linux](https://github.com/xeroz369/bk-study-desk#bản-cho-macos-và-linux-thử-nghiệm) trong README: cách chạy, phần đã và chưa kiểm, chỗ khác với bản Windows.
