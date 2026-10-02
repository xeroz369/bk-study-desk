# Đóng góp

**Tiếng Việt** · [English](#english)

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
- **Tài liệu hai ngôn ngữ:** sửa phần tiếng Việt thì sửa luôn phần tiếng Anh (mục **English** ở cuối file, trang Wiki tiếng Anh) trong cùng PR.
- **Không đoán:** mẫu giao diện, giới hạn API, con số thời gian chờ… phải dựa trên tài liệu gốc (Microsoft Learn, mã nguồn, RFC) hoặc đo thật, và ghi nguồn trong comment. Câu nào trong README/Wiki hứa điều gì thì code phải làm đúng điều đó.
- **Commit** ngắn gọn, theo [Conventional Commits](https://www.conventionalcommits.org/): `feat: …`, `fix: …`, `docs: …`.

## Kiểm thử

- **Test tự động** không gọi máy chủ thật của trường (LMS, MyBK, SSO) và không dùng tài khoản, cookie hay token thật.
- **Lỗi mạng và máy chủ** (429 kèm `Retry-After`, 5xx, timeout, mất mạng, JSON hỏng) giả lập bằng `HttpMessageHandler` giả hoặc mock server, kèm dữ liệu mẫu đã ẩn danh (không tên, MSSV, token).
- **Logic phụ thuộc thời gian** dùng `TimeProvider`; test không `sleep`.
- **Test giao diện** tìm control theo AutomationId và dùng UI Automation pattern (Invoke/Toggle/Value), không bấm theo tọa độ.
- **Thử thật trên LMS/MyBK** chỉ do người làm bằng tay, chỉ đọc, trước khi phát hành.

## Giấy phép

Gửi PR nghĩa là bạn đồng ý phần đóng góp theo cùng giấy phép của dự án ([PolyForm Noncommercial 1.0.0](LICENSE.md)).

---

## English

<details>
<summary><b>Contributing</b> (English version)</summary>

Issues and pull requests are very welcome, especially when the university changes its pages or APIs. You can write issues in Vietnamese or English; keep technical terms in English.

## Before you open a pull request

```powershell
dotnet format src/SoHocTap --verify-no-changes --severity warn
dotnet build src/SoHocTap -c Release -warnaserror
dotnet test tests/SoHocTap.Tests
```

If you change the Practice view (`src/ui`), also run `npm run check` and `npx prettier --check src`. CI runs exactly these commands.

## Rules

- **Read-only:** no feature may submit work, register, cancel, pay or change any data on university systems.
- **No personal data** (names, student IDs, tokens, machine paths) in code, images, logs or issues.
- **Simple code:** short functions that do one thing, clear names. Comments explain *why*; English, or Vietnamese with English technical terms, are both fine.
- **UI text** lives in `src/SoHocTap/lang/*.json`: write `vi.json` first, then `en.json`, and keep every `{0}` placeholder.
- **Style:** follow [docs/van-phong.md](docs/van-phong.md) (Vietnamese) and [docs/english-style.md](docs/english-style.md) (English) for terms, capitalization, punctuation, error messages and the changelog. Every rule there cites a source.
- **Two languages:** when you change the Vietnamese part of a document, update its English part (the **English** section at the end of the file, the English Wiki page) in the same pull request.
- **No guessing:** UI patterns, API limits, timeouts and similar numbers must come from primary sources (Microsoft Learn, source code, RFCs) or real measurements, cited in a comment. Anything the README or Wiki promises, the code must actually do.
- **Commits** are short and follow [Conventional Commits](https://www.conventionalcommits.org/): `feat: …`, `fix: …`, `docs: …`.

## Testing

- **Automated tests** never call the university's real servers (LMS, MyBK, SSO) and never use real accounts, cookies or tokens.
- **Network and server failures** (429 with `Retry-After`, 5xx, timeouts, no connection, broken JSON) are simulated with a fake `HttpMessageHandler` or a mock server, using anonymized sample data (no names, student IDs or tokens).
- **Time-dependent logic** uses `TimeProvider`; tests don't `sleep`.
- **UI tests** find controls by AutomationId and use UI Automation patterns (Invoke, Toggle, Value), not screen coordinates.
- **Real checks on LMS/MyBK** are done by hand by the maintainer, read-only, before a release.

## License

By opening a pull request you agree to license your contribution under the project's license ([PolyForm Noncommercial 1.0.0](LICENSE.md)).

</details>
