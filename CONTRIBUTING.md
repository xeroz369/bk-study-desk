# Đóng góp

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](CONTRIBUTING.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](docs/en/CONTRIBUTING.md)

Rất hoan nghênh issue và PR, nhất là khi trường đổi giao diện hay API. Issue cứ viết tiếng Việt; thuật ngữ kỹ thuật giữ tiếng Anh.

## Viết vào đâu

Xác định chỗ viết trước khi code, đừng vá tạm vào file gần nhất.

| Việc | Viết ở |
|---|---|
| Đọc dữ liệu mới từ LMS, MyBK | `BKStudyDesk.Core/Sources/Lms`, `Sources/Mybk` (một file một nhóm API) |
| Bản ghi, lưu file dữ liệu | `BKStudyDesk.Core/Data/` |
| Quy tắc thuần (giờ VN, mức gấp, tách mã nhóm) | `BKStudyDesk.Core/Core/` hoặc `Ui/Due.cs`, có test |
| Khóa cấu hình mới | `BKStudyDesk.Core/Core/DefaultConfig.json` và một thuộc tính trong `Core/Settings.cs` |
| Trang hiện mục nào, thứ tự, chữ trong dòng; dải báo; mục Cài đặt | `BKStudyDesk.Core/Presentation/` (thuần, có test) |
| Bố cục một trang | `BKStudyDesk.Desktop/Views/<Trang>View.axaml` |
| Khung cửa sổ, thanh trên cùng, dải báo, bảng Thông báo | `BKStudyDesk.Desktop/MainWindow.axaml(.cs)` |
| Đăng nhập, WebView, MyBK chạy ẩn | `BKStudyDesk.Desktop/Web/` |
| Khay, mở cùng máy, kho mật khẩu, bản Store, thông báo hệ thống | `BKStudyDesk.Desktop/Platform/` (`Platform/Windows/` chỉ biên dịch cho Windows) |
| Luyện tập: chấm điểm, lịch ôn, gói, nhập xuất | `BKStudyDesk.Core/Presentation/Practice/`, có test |
| Luyện tập: màn, nút | `BKStudyDesk.Desktop/Views/Practice/` |
| Màu, cỡ, số đo khung | `BKStudyDesk.Desktop/Styles/Tokens.axaml`, quy tắc ở `design-system/bk/README.md` |
| Chữ hiển thị | `BKStudyDesk.Core/lang/vi.json` và `lang/en.json` |

Chưa có chỗ phù hợp thì tạo file mới nhỏ, một việc, thay vì nhét vào file lớn.

## Cấu trúc thư mục

```
src/
├── BKStudyDesk.Desktop/ app (Avalonia 12, theme Fluent có sẵn; Windows, macOS, Linux)
│   ├── Views/           các trang, hộp thoại, Luyện tập (Practice/)
│   ├── Web/             đăng nhập, WebView, MyBK chạy ẩn
│   ├── Platform/        khay, mở cùng máy, kho mật khẩu, thông báo (Windows/ chỉ cho Windows)
│   ├── Updates/         kiểm tra và tải bản cập nhật (Velopack)
│   └── Styles/          token màu, cỡ (Tokens.axaml)
├── BKStudyDesk.Core/    lõi, không phụ thuộc giao diện
│   ├── Core/            config, đường dẫn, log, quy tắc thuần
│   ├── Data/            bản ghi và lưu file dữ liệu
│   ├── Sources/         đọc LMS (Lms/) và MyBK (Mybk/)
│   ├── Library/         thư viện tài liệu
│   ├── Files/           xếp và đặt tên file tải về
│   ├── Api/             API nội bộ (Luyện tập đọc ghi kết quả, gói)
│   ├── Presentation/    logic từng trang, dải báo, Cài đặt, Luyện tập
│   ├── Ui/              trạng thái app, định dạng, chữ (không có giao diện)
│   └── lang/            chữ giao diện vi.json, en.json
├── ThirdParty/XamlMath/ bộ vẽ công thức TeX (MIT, tự bảo trì)
└── BKStudyDesk.Setup/   bộ cài, bộ gỡ tiếng Việt
studypack/               đặc tả và công cụ gói luyện tập (Node)
tests/                   xUnit: BKStudyDesk.Logic.Tests, BKStudyDesk.Core.Tests, BKStudyDesk.Math.Tests
```

## Phân lớp

- `BKStudyDesk.Core` không dùng Avalonia hay API riêng của hệ điều hành ngoài nhánh `OperatingSystem.IsWindows()`.
- View không lọc, sắp hay quyết định hiện gì: hỏi `Presentation/`. View không gọi mạng trực tiếp: đi qua `AppHost` và các service.
- Không icon, không control tự viết, không viết lại template control (`design-system/bk/README.md`).
- Đây là quy tắc đích. Chỗ nào còn vi phạm thì đừng làm tệ thêm.
- `BKStudyDesk.Logic.Tests` tự lấy mọi file trong `Core/`, `Data/`, `Sources/`, `Library/` của lõi. File cần `Config`, `Paths` thì ghi vào `LogicExclude` trong `BKStudyDesk.Logic.Tests.csproj` kèm lý do.

## Thêm một mục Cài đặt

1. Thêm khóa vào `Core/DefaultConfig.json` và một thuộc tính trong `Core/Settings.cs`.
2. Thêm một dòng vào `Presentation/SettingsCatalog.cs` (`ToggleItem`, `NumberItem`, `ChoiceItem`...), đúng thẻ; ít khi dùng thì vào nhóm Nâng cao. `SettingsView` tự vẽ, không cần sửa.
3. Mục chỉ đọc và ghi qua `Settings`, không gọi `Config` trực tiếp.
4. Chữ hiển thị thêm vào `lang/vi.json` rồi `lang/en.json`.

## Thêm một cột vào bảng sự kiện

1. Giá trị cột là thuộc tính của dòng trong `Presentation/` (ví dụ `CalendarRow` ở `Presentation/Calendar.cs`), dựng từ `TimelineItem` (`Ui/AppState.cs`) và bản ghi trong `Data/Models.cs`; trường mới thì thêm ở cả hai.
2. Quy tắc tính giá trị (mức gấp, tách nhóm) viết thuần ở `Core/` hoặc `Ui/Due.cs`, có test.
3. Cột thêm vào `DataGrid` trong `Views/<Trang>View.axaml`: cột ngắn `Width="Auto"` kèm `MinWidth`, cột tên chia phần còn lại; không đặt độ rộng cứng làm mất chữ.
4. Tiêu đề cột thêm vào `lang/vi.json` rồi `lang/en.json`.

## Trước khi gửi PR

```powershell
dotnet format src/BKStudyDesk.Core/BKStudyDesk.Core.csproj --verify-no-changes --severity warn
dotnet format src/BKStudyDesk.Desktop/BKStudyDesk.Desktop.csproj --verify-no-changes --severity warn
dotnet build src/BKStudyDesk.Desktop -c Release -f net10.0-windows10.0.19041.0 -warnaserror
dotnet test tests/BKStudyDesk.Logic.Tests
dotnet test tests/BKStudyDesk.Core.Tests
dotnet test tests/BKStudyDesk.Math.Tests
```

Sửa giao diện thì chụp lại bằng `--snap=<ảnh.png>` (app tự vẽ ra ảnh, dùng dữ liệu demo), sáng và tối (`--theme=light`). CI chạy đúng các lệnh này trên Windows, macOS, Linux.

## Quy tắc

- **Chỉ đọc:** không tính năng nào được nộp bài, đăng ký, hủy, thanh toán hay sửa dữ liệu trên hệ thống của trường.
- **Không dữ liệu cá nhân** (tên, MSSV, token, đường dẫn máy) trong mã, ảnh, log hay issue.
- **Code đơn giản:** hàm ngắn, làm một việc, tên rõ nghĩa. Comment giải thích *vì sao*, tiếng Anh hoặc tiếng Việt kèm thuật ngữ đều được.
- **Chữ trên giao diện** nằm trong `src/BKStudyDesk.Core/lang/*.json`: viết `vi.json` trước, giữ đủ chỗ trống `{0}`.
- **Văn phong** theo [docs/van-phong.md](docs/van-phong.md) (tiếng Việt) và [docs/english-style.md](docs/english-style.md) (tiếng Anh): thuật ngữ, viết hoa, dấu câu, thông báo lỗi, CHANGELOG. Mỗi quy tắc ở đó có nguồn.
- **Tài liệu hai ngôn ngữ:** sửa phần tiếng Việt thì sửa luôn phần tiếng Anh (file cùng tên trong `docs/en/`, trang Wiki tiếng Anh) trong cùng PR.
- **Không đoán:** mẫu giao diện, giới hạn API, con số thời gian chờ... phải dựa trên tài liệu gốc (Microsoft Learn, mã nguồn, RFC) hoặc đo thật, và ghi nguồn trong comment. Câu nào trong README/Wiki hứa điều gì thì code phải làm đúng điều đó.
- **Commit** ngắn gọn, theo [Conventional Commits](https://www.conventionalcommits.org/): `feat: ...`, `fix: ...`, `docs: ...`.

## Kiểm thử

- **Test tự động** không gọi máy chủ thật của trường (LMS, MyBK, SSO) và không dùng tài khoản, cookie hay token thật.
- **Lỗi mạng và máy chủ** (429 kèm `Retry-After`, 5xx, timeout, mất mạng, JSON hỏng) giả lập bằng `HttpMessageHandler` giả hoặc mock server, kèm dữ liệu mẫu đã ẩn danh (không tên, MSSV, token).
- **Logic phụ thuộc thời gian** dùng `TimeProvider`; test không `sleep`.
- **Test giao diện** tìm control theo AutomationId và dùng UI Automation pattern (Invoke/Toggle/Value), không bấm theo tọa độ.
- **Thử thật trên LMS/MyBK** chỉ do người làm bằng tay, chỉ đọc, trước khi phát hành.

## Giấy phép

Gửi PR nghĩa là bạn đồng ý phần đóng góp theo cùng giấy phép của dự án ([AGPL-3.0](LICENSE.md)).