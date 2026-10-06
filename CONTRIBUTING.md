# Đóng góp

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](CONTRIBUTING.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](docs/en/CONTRIBUTING.md)

Rất hoan nghênh issue và PR, nhất là khi trường đổi giao diện hay API. Issue cứ viết tiếng Việt; thuật ngữ kỹ thuật giữ tiếng Anh.

## Viết vào đâu

Xác định chỗ viết trước khi code, đừng vá tạm vào file gần nhất.

| Việc | Viết ở |
|---|---|
| Đọc dữ liệu mới từ LMS, MyBK | `Sources/Lms`, `Sources/Mybk` (một file một nhóm API) |
| Bản ghi, lưu file dữ liệu | `Data/` |
| Quy tắc thuần (giờ VN, mức gấp, tách mã nhóm) | `Core/` hoặc `Ui/Due.cs`, có test |
| Khóa cấu hình mới | `Core/DefaultConfig.json` và một thuộc tính trong `Core/Settings.cs` |
| Thẻ Cài đặt | một file trong `Ui/Settings/`, một dòng trong `SettingsSections.cs` |
| Cột hay kiểu hiển thị của bảng sự kiện | `Ui/Controls/TimelineList` |
| Trang mới | `Ui/Pages/`, đăng ký trong `PageRegistry` |
| Cửa sổ, khay, đăng nhập, WebView | `Shell/` |
| Chữ hiển thị | `lang/vi.json` và `lang/en.json` |

Chưa có chỗ phù hợp thì tạo file mới nhỏ, một việc, thay vì nhét vào file lớn.

## Cấu trúc thư mục

```
src/
├── SoHocTap/            app WPF (.NET 10, theme Fluent)
│   ├── Core/            config, đường dẫn, log, quy tắc thuần
│   ├── Data/            bản ghi và lưu file dữ liệu
│   ├── Sources/         đọc LMS (Lms/) và MyBK (Mybk/)
│   ├── Library/         thư viện tài liệu
│   ├── Files/           xếp và đặt tên file tải về
│   ├── Updates/         kiểm tra và tải bản cập nhật
│   ├── Api/             API nội bộ cho khung Luyện tập
│   ├── Shell/           cửa sổ, khay, nhắc hạn, đăng nhập
│   ├── Ui/              giao diện WPF (Pages/, Controls/)
│   ├── lang/            chữ giao diện vi.json, en.json
│   └── tools-dev/       script phát hành, export
├── ui/                  khung Luyện tập (Svelte), build ra ui/
├── BKStudyDesk.Core/    lõi đa nền tảng, chỉ link file từ SoHocTap
├── BKStudyDesk.Desktop/ bản Avalonia thử nghiệm (Linux)
└── BKStudyDesk.Setup/   bộ cài
tests/SoHocTap.Tests/    xUnit
```

`Ui/Format.cs`, `Ui/AppState.cs`, `Ui/Lang.cs`, `Ui/Models.cs`, `Ui/Curriculum.cs`, `Ui/Due.cs` cũng được biên dịch vào `BKStudyDesk.Core`: giữ chúng không phụ thuộc WPF.

## Phân lớp

- `Core/`, `Data/`, `Sources/`, `Library/`, `Files/`, `Updates/` không dùng WPF.
- `Ui/` không gọi mạng trực tiếp: đi qua `AppHost` và các service.
- `Shell/` không phụ thuộc trang cụ thể: trang đăng ký qua `PageRegistry`.
- Đây là quy tắc đích. Chỗ nào còn vi phạm thì đừng làm tệ thêm.
- Test tự lấy mọi file trong `Core/`, `Data/`, `Sources/`, `Library/`. File cần `Config`, `Paths` hay WPF thì ghi vào `LogicExclude` trong `SoHocTap.Tests.csproj` kèm lý do.

## Thêm một thẻ Cài đặt

1. Thêm khóa vào `Core/DefaultConfig.json` và một thuộc tính trong `Core/Settings.cs`.
2. Tạo `Ui/Settings/<Tên>Section.xaml` và `.xaml.cs`, cài `ISettingsSection`.
3. Thẻ chỉ đọc và ghi qua `Settings`, không gọi `Config` trực tiếp.
4. Thêm một dòng vào `Ui/Settings/SettingsSections.cs`: `Common` nếu người dùng hay cần, `Advanced` (mục Nâng cao, gập sẵn) nếu ít khi dùng.
5. Chữ hiển thị thêm vào `lang/vi.json` rồi `lang/en.json`.

## Thêm một cột vào bảng sự kiện

1. Cột nằm trong `Ui/Controls/TimelineList`, dùng chung cho mọi bảng sự kiện.
2. Dữ liệu cột là thuộc tính của `TimelineItem` (`Ui/AppState.cs`), dựng từ bản ghi trong `Data/Models.cs`; trường mới thì thêm ở cả hai.
3. Quy tắc tính giá trị (mức gấp, tách nhóm) viết thuần ở `Core/` hoặc `Ui/Due.cs`, có test.
4. Tiêu đề cột thêm vào `lang/vi.json` rồi `lang/en.json`.
5. Tạo cột bằng `Grids.Text`, `Grids.Flex` hay `Grids.Right` để độ rộng giãn theo cỡ chữ đang chọn; không đặt độ rộng cứng làm mất chữ (DESIGN 6b-3).

## Trước khi gửi PR

```powershell
dotnet format src/SoHocTap --verify-no-changes --severity warn
dotnet build src/SoHocTap -c Release -warnaserror
dotnet test tests/SoHocTap.Tests
```

Sửa khung Luyện tập (`src/ui`) thì chạy thêm `npm run check` và `npx prettier --check src`. CI chạy đúng các lệnh này.

## Quy tắc

- **Chỉ đọc:** không tính năng nào được nộp bài, đăng ký, hủy, thanh toán hay sửa dữ liệu trên hệ thống của trường.
- **Không dữ liệu cá nhân** (tên, MSSV, token, đường dẫn máy) trong mã, ảnh, log hay issue.
- **Code đơn giản:** hàm ngắn, làm một việc, tên rõ nghĩa. Comment giải thích *vì sao*, tiếng Anh hoặc tiếng Việt kèm thuật ngữ đều được.
- **Chữ trên giao diện** nằm trong `src/SoHocTap/lang/*.json`: viết `vi.json` trước, giữ đủ chỗ trống `{0}`.
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