# BK Study Desk

![BK Study Desk](docs/hero.png)

App Windows nhỏ gọn cho sinh viên **Bách Khoa TP.HCM (HCMUT)**. App gom **BK-LMS** và **MyBK** vào một cửa sổ:
- deadline, quiz;
- thời khóa biểu, lịch thi;
- tài liệu từng môn, tải sẵn về máy;
- điểm (sổ điểm LMS lẫn điểm thành phần trên MyBK);
- tiến độ chương trình đào tạo;
- shortcut tới khoảng 30 dịch vụ của trường.

> **Không chính thức.** Đây là project cá nhân, không liên quan và không được trường bảo trợ. App chỉ đọc những gì bạn vốn xem được sau khi login.

UI mặc định tiếng Việt, có thêm tiếng Anh. *English summary at the bottom.*

## Cài đặt

Có 2 cách, chọn một:

### Cách 1: Microsoft Store (khuyên dùng)

**[Tải BK Study Desk trên Microsoft Store](https://apps.microsoft.com/detail/9NX0KRZRR850)**

> App đang chờ Microsoft duyệt. Link sẽ dùng được khi app được duyệt xong.

- Mở link trên, bấm **Tải / Get** là xong.
- Store tự cập nhật, không có cảnh báo SmartScreen.
- Tài liệu mặc định lưu vào `Documents\BK Study Desk`. Có thể đổi folder trong Cài đặt.

### Cách 2: Tải file zip trên GitHub

1. Tải file `BKStudyDesk-x.y.z-win-x64.zip` mới nhất ở [Releases](../../releases).
2. Check xem file tải về có bị lỗi không. Mở PowerShell ở folder chứa file zip và chạy lệnh dưới. Kết quả phải giống mã SHA-256 ghi trong trang Releases (hoặc trong `SHA256SUMS.txt`).
   ```powershell
   (Get-FileHash .\BKStudyDesk-x.y.z-win-x64.zip -Algorithm SHA256).Hash
   ```
3. Giải nén vào chỗ bạn có quyền ghi, ví dụ `Documents\BK Study Desk`.
4. Chạy `app\BKStudyDesk.exe`.
   - SmartScreen có thể cảnh báo vì exe chưa được ký số. Chọn *Thông tin thêm → Vẫn chạy*.

**Cập nhật bản zip:** thoát app (chuột phải tray icon → Thoát), rồi giải nén bản mới đè lên folder cũ. Folder `data\` vẫn giữ nguyên.

### Sau khi cài

Mở app, bấm **Đăng nhập HCMUT**, rồi login trên trang SSO của trường. Một lần login dùng được cho cả LMS và MyBK.

**Yêu cầu:**
- Windows 10 1809 trở lên, hoặc Windows 11 (x64).
- Microsoft Edge WebView2 Runtime (Windows 11 có sẵn).
- Không cần cài .NET.

## Tính năng

- **Hôm nay**:
  - đếm ngược tới kỳ thi gần nhất;
  - deadline trong 7 ngày và quiz;
  - thông báo mới trên LMS.
- **Lịch**: việc sắp tới theo ngày, thời khóa biểu tuần, lịch thi.
- **Môn học**:
  - duyệt folder từng môn kiểu File Explorer;
  - xem file mới, deadline, thông báo, sổ điểm và các lớp trên LMS.
  - **Tải tài liệu theo từng mục** của course LMS, tùy chọn tự giải nén .zip/.rar/.7z.
  - Có thể bật auto-download để mỗi lần sync app tải luôn file mới.
- **Điểm và học vụ**:
  - bảng điểm theo học kỳ kèm điểm thành phần;
  - tiến độ CTĐT theo khối kiến thức;
  - sổ điểm LMS, kết quả đăng ký môn, ngày CTXH, quyết định học vụ.
- **Dịch vụ**: mở MyBK, LMS, BKPay, đăng ký môn… ngay trong app, dùng chung một lần login.
- **Login một lần**: SSO HCMUT một lần là vào được cả LMS và MyBK. Session còn hạn thì app tự login lại.
- **Dùng như app Windows thật**:
  - theme Windows 11 (Fluent), tự theo sáng/tối và màu nhấn;
  - bảng sort được, menu chuột phải;
  - phím tắt Ctrl+1…6, F5, Alt+←;
  - tray icon, nhắc deadline;
  - autostart cùng Windows;
  - bấm X thì thu xuống tray hoặc thoát hẳn, tùy bạn chọn.
- **Chọn folder lưu tài liệu** trong Cài đặt.
- **Ngôn ngữ**: tiếng Việt (gốc) và tiếng Anh. Muốn thêm ngôn ngữ thì thả một file JSON vào `lang\`, xem [`src/SoHocTap/lang/README.md`](src/SoHocTap/lang/README.md).

## Bảo mật và quyền riêng tư

Chính sách đầy đủ: [PRIVACY.md](PRIVACY.md).


- Password chỉ gõ trên trang SSO của trường. App không đọc, không lưu password.
- Mọi thứ nằm trong folder `data\` (bản zip: cạnh app; bản Store: trong `%LOCALAPPDATA%`, gỡ app là xóa luôn):
  - cookie trong `data\webview`, do WebView2 tự encrypt;
  - token LMS trong `data\secrets\`, **encrypt bằng Windows DPAPI**. Copy sang máy khác hay user Windows khác thì không dùng được.
- Folder `data\` được khóa ACL, chỉ user Windows của bạn (cùng SYSTEM và Administrators) mở được.
- Có thể tắt **Ghi nhớ đăng nhập** trong Cài đặt. Khi đó mỗi lần mở app phải login lại.
- Giới hạn: malware chạy dưới chính user Windows của bạn vẫn có thể đọc được session. App nào trên Windows cũng vậy. Nghi máy dính malware thì bấm **Đăng xuất** rồi đổi password HCMUT.
- App chỉ gọi API **đọc** và không gửi dữ liệu đi đâu ngoài server của trường. App không bao giờ nộp bài, đăng ký, hủy, thanh toán hay làm quiz thay bạn.
- App không lưu CCCD, địa chỉ, số điện thoại, ngày sinh hay email cá nhân mà MyBK trả về.
- Gọi server trường càng ít càng tốt:
  - chưa login thì không gọi gì;
  - LMS chỉ hỏi phần thay đổi, khoảng 20 request mỗi 3 giờ;
  - MyBK sync 12 giờ một lần;
  - session hết hạn thì chờ hết chu kỳ mới retry.

## Tự build

```powershell
dotnet build src/SoHocTap -c Release
dotnet publish src/SoHocTap -c Release -r win-x64 --self-contained -o publish
```

Cần .NET 10 SDK và Windows. Cấu trúc code:

| Folder | Nội dung |
|---|---|
| `src/SoHocTap/Core` | path, config (`Core/DefaultConfig.json`), lưu JSON, log, encrypt secret |
| `src/SoHocTap/Sources` | connector LMS (Moodle mobile web service), MyBK, scheduler sync |
| `src/SoHocTap/Files` | folder môn học, tìm file trùng, giải nén |
| `src/SoHocTap/Shell` | login, WebView ẩn chạy MyBK, tray, thông báo, autostart |
| `src/SoHocTap/Ui` | UI WPF (`MainWindow`, `Pages/`), load language pack |
| `src/SoHocTap/lang` | language pack (`vi.json`, `en.json`) |

Mọi URL của trường, API path, tên folder và chu kỳ sync đều nằm trong config, không hardcode. Xem `Core/DefaultConfig.json`. Lần chạy đầu, app copy file này thành `data\config.json`.

## Đóng góp

Issue và PR đều welcome, nhất là khi trường đổi giao diện hay API. Issue cứ viết tiếng Việt, thuật ngữ kỹ thuật để tiếng Anh cho dễ hiểu.

**Tiêu chuẩn code:**
- **C#:** theo [quy ước C# của Microsoft](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions) và `.editorconfig` trong repo. Trước khi gửi PR, chạy:
  ```powershell
  dotnet format src/SoHocTap --verify-no-changes --severity warn
  dotnet build src/SoHocTap -c Release -warnaserror
  ```
  CI cũng chạy đúng hai lệnh này.
- **Code đơn giản, dễ đọc:** hàm ngắn, làm một việc, tên rõ nghĩa. Không thêm lớp trừu tượng khi chưa thật sự cần.
- **Comment ngắn**, giải thích *vì sao* chứ không kể lại code làm gì. Tiếng Anh, hoặc tiếng Việt kèm thuật ngữ tiếng Anh, đều được.
- **Chữ trên giao diện** nằm trong `src/SoHocTap/lang/*.json`, viết `vi.json` trước.
- **Commit** theo [Conventional Commits](https://www.conventionalcommits.org/): `feat: …`, `fix: …`, `docs: …`.

Nhớ giữ quy tắc **chỉ đọc**: không tính năng nào được submit form hay sửa dữ liệu trên hệ thống của trường.

## License

[MIT](LICENSE)

---

## English

**BK Study Desk** is an unofficial Windows desktop app for HCMUT students. It combines BK-LMS and MyBK in one window:
- deadlines and quizzes;
- timetable and exam schedule;
- course documents, downloadable section by section;
- grades and curriculum progress;
- shortcuts to school services.

**Language:** Vietnamese is the default UI language; English is available in Settings.

**Install:** get it from the Microsoft Store (link coming after review), or download the zip from Releases, verify its SHA-256, extract it, then run `app\BKStudyDesk.exe`.

**Privacy:** see [PRIVACY.md](PRIVACY.md). The app is read-only. Your password is typed only on the school's SSO page. The LMS token is DPAPI-encrypted, and the data folder is restricted to your Windows account.
