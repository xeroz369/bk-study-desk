# Đóng góp

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
- **Commit** ngắn gọn, theo [Conventional Commits](https://www.conventionalcommits.org/): `feat: …`, `fix: …`, `docs: …`.

## Giấy phép

Gửi PR nghĩa là bạn đồng ý phần đóng góp theo cùng giấy phép của dự án ([PolyForm Noncommercial 1.0.0](LICENSE)).