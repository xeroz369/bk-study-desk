# Kế hoạch: BK Study Desk chạy trên Windows, Linux, macOS

Bắt đầu: 02/10/2026. App WPF hiện tại **vẫn là bản chính** cho tới khi bản mới làm được đủ việc. Không sửa hay xóa nó trong lúc chuyển.

## Chọn framework: Avalonia UI 12 (C#) + Avalonia.Controls.WebView (MIT)

- Chạy trên Windows, Linux, macOS. XAML gần giống WPF nên các trang chuyển khá thẳng.
- Giữ được khoảng 60–70% code: đồng bộ LMS/MyBK, xếp file, API nội bộ, config, mô hình dữ liệu, khung Luyện tập (Svelte).
- Nhẹ, không kèm cả trình duyệt như Electron.
- Đã cân nhắc và bỏ:
  - Tauri: phải viết lại phần đồng bộ bằng Rust.
  - Electron: nặng, tốn pin.
  - .NET MAUI: không chạy trên Linux.

## Những gì đang dính vào Windows và cách thay

| Phần | Hiện tại (Windows) | Trên cả 3 hệ điều hành |
|---|---|---|
| Giao diện | WPF + Fluent | Avalonia + theme Fluent |
| Trình duyệt nhúng (đăng nhập SSO, chạy MyBK ngầm, khung Luyện tập) | WebView2 | WebView của hệ điều hành: WebView2 / WKWebView / WebKitGTK, qua một control Avalonia. **Rủi ro lớn nhất, phải thử trước (bước 3).** |
| Token LMS | DPAPI | Windows: DPAPI · macOS: Keychain · Linux: libsecret (Secret Service) |
| Khóa quyền thư mục data | ACL của Windows | Linux/macOS: `chmod 700` |
| Tự chạy khi khởi động | Registry Run / StartupTask | Linux: `~/.config/autostart/*.desktop` · macOS: LaunchAgent |
| Khay hệ thống, thông báo | WinForms NotifyIcon | `TrayIcon` có sẵn của Avalonia; thông báo của từng hệ điều hành |
| Mở file, link | ShellExecute | `xdg-open` / `open` / ShellExecute |

## Các bước

1. **Tách lõi chung** (`src/BKStudyDesk.Core`, net10.0, không phụ thuộc Windows): Core, Files, Sources, Api, mô hình dữ liệu. Project mới dùng *link* tới file nguồn cũ, không di chuyển file, để app WPF vẫn build như cũ. Những chỗ dính Windows (DPAPI, ACL, registry, P/Invoke) đi qua interface có bản Windows và bản Unix.
   - Xong khi: lõi build được cho `win-x64`, `linux-x64`, `osx-arm64`.
2. **Khung app Avalonia** (`src/BKStudyDesk.Desktop`): cửa sổ, thanh điều hướng, thanh trạng thái, các trang chỉ đọc (Hôm nay, Lịch, Môn học, Điểm, Giới thiệu), đọc `data\` có sẵn.
   - Xong khi: chạy được trên Windows và hiện đúng dữ liệu đã đồng bộ; build được cho Linux và macOS.
3. **Thử trình duyệt nhúng** trên cả 3 hệ điều hành:
   - mở trang SSO, đọc cookie, bắt link `moodlemobile://token=…`;
   - nhúng khung Luyện tập với host ảo và cầu nối `/api`.
   - Không đạt thì phương án dự phòng: đăng nhập bằng trình duyệt ngoài, mở khung Luyện tập bằng trình duyệt ngoài qua `http://127.0.0.1` cùng token phiên.
4. **Đăng nhập và đồng bộ**: LoginFlow, MybkRunner chạy trên WebView mới; lưu token theo từng hệ điều hành.
5. **Khung Luyện tập** trong WebView mới; trang Cài đặt; khay hệ thống, thông báo, tự chạy khi khởi động.
6. **Đóng gói**:
   - Windows: zip và MSIX như bây giờ;
   - Linux: AppImage hoặc tar.gz;
   - macOS: `.app` trong `.dmg`. Cần ký và notarize bằng tài khoản Apple Developer, không thì macOS chặn mở.
7. **Chuyển hẳn**: khi bản Avalonia làm được mọi việc của bản WPF, đổi bản chính sang Avalonia.

## Giới hạn cần biết

- **macOS:** muốn phát hành cho người khác cần tài khoản Apple Developer (99 USD/năm) để ký app. Không có thì người dùng phải tự bỏ chặn.
- **Kiểm thử:** máy này chỉ có Windows. Linux thử được qua WSL, còn macOS cần máy Mac thật hoặc CI macOS của GitHub.

## Tiến độ

- [x] Bước 0: kế hoạch này
- [x] Bước 1: tách lõi chung (`src/BKStudyDesk.Core`): build được cho win-x64, linux-x64, osx-arm64; app WPF vẫn build như cũ.
- [~] Bước 2: khung app Avalonia (`src/BKStudyDesk.Desktop`) với các trang chỉ đọc Hôm nay, Lịch, Môn học, Điểm, Giới thiệu.
  - Chạy được trên Windows (đọc đúng data demo) và trên Linux (thử qua WSL Debian). Build được cho macOS (osx-arm64, osx-x64) nhưng chưa chạy thử, vì không có máy Mac.
  - ICU đi kèm app, nên Linux không cần cài libicu.
  - Còn lại: theme, trang Cài đặt.
- [ ] Bước 3: thử trình duyệt nhúng
- [ ] Bước 4–7
- [~] Bước 6 (một phần, 02/10/2026): bộ cài và tự cập nhật bằng Velopack (spec `docs/specs/2026-10-02-bo-cai-tu-cap-nhat.md`).
  - Windows: `Setup.exe` x64/ARM64, cập nhật delta, người dùng tự chọn chế độ cập nhật; test `tools-dev/test-update.ps1` 14/14 PASS.
  - Linux: AppImage chạy được (WSL), dữ liệu ở `~/.local/share/BKStudyDesk`; cần `libfuse2`. Cập nhật bấm tay ở trang Giới thiệu, chưa test cập nhật thật trên Linux.
  - macOS: để sau (ít người dùng).
