# Đóng góp

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](CONTRIBUTING.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](CONTRIBUTING.en.md)

Rất hoan nghênh issue và PR, nhất là khi trường đổi giao diện hay API. Issue cứ viết tiếng Việt; thuật ngữ kỹ thuật giữ tiếng Anh.

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
- **Tài liệu hai ngôn ngữ:** sửa phần tiếng Việt thì sửa luôn phần tiếng Anh (file `.en.md` cùng tên, trang Wiki tiếng Anh) trong cùng PR.
- **Không đoán:** mẫu giao diện, giới hạn API, con số thời gian chờ... phải dựa trên tài liệu gốc (Microsoft Learn, mã nguồn, RFC) hoặc đo thật, và ghi nguồn trong comment. Câu nào trong README/Wiki hứa điều gì thì code phải làm đúng điều đó.
- **Commit** ngắn gọn, theo [Conventional Commits](https://www.conventionalcommits.org/): `feat: ...`, `fix: ...`, `docs: ...`.

## Kiểm thử

- **Test tự động** không gọi máy chủ thật của trường (LMS, MyBK, SSO) và không dùng tài khoản, cookie hay token thật.
- **Lỗi mạng và máy chủ** (429 kèm `Retry-After`, 5xx, timeout, mất mạng, JSON hỏng) giả lập bằng `HttpMessageHandler` giả hoặc mock server, kèm dữ liệu mẫu đã ẩn danh (không tên, MSSV, token).
- **Logic phụ thuộc thời gian** dùng `TimeProvider`; test không `sleep`.
- **Test giao diện** tìm control theo AutomationId và dùng UI Automation pattern (Invoke/Toggle/Value), không bấm theo tọa độ.
- **Thử thật trên LMS/MyBK** chỉ do người làm bằng tay, chỉ đọc, trước khi phát hành.

## Giấy phép

Gửi PR nghĩa là bạn đồng ý phần đóng góp theo cùng giấy phép của dự án ([PolyForm Noncommercial 1.0.0](LICENSE.md)).