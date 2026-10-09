## Thay đổi gì

<!-- Một hai câu: sửa gì, vì sao. -->

## Đã kiểm

- [ ] `dotnet format src/BKStudyDesk.Desktop/BKStudyDesk.Desktop.csproj --verify-no-changes --severity warn` (cả `src/BKStudyDesk.Core`)
- [ ] `dotnet build src/BKStudyDesk.Desktop -c Release -f net10.0-windows10.0.19041.0 -warnaserror`
- [ ] `dotnet test` cho `tests/BKStudyDesk.Logic.Tests`, `tests/BKStudyDesk.Core.Tests`, `tests/BKStudyDesk.Math.Tests`
- [ ] Sửa giao diện: đã chụp sáng/tối bằng `--snap` (dữ liệu demo)
- [ ] Vẫn giữ quy tắc **chỉ đọc** (không nộp bài, không sửa dữ liệu trên hệ thống của trường)
- [ ] Không có dữ liệu cá nhân (tên, MSSV, token, đường dẫn máy) trong mã, ảnh, log