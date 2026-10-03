# studypack: quiz tự soạn

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](README.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](../docs/en/studypack/README.md)

Soạn quiz để tự ôn và gửi cho bạn bè: gõ trong app, nhờ AI hoặc đổi qua lại với Moodle (BK-LMS).

| File | Dùng để |
|---|---|
| [Wiki: Luyện tập](https://github.com/bk-study-desk/bk-study-desk/wiki/Luyện-tập) | **Bắt đầu ở đây**: hướng dẫn từng bước, có ảnh, không cần biết lập trình |
| [SPEC.md](SPEC.md) | Đặc tả đầy đủ của Study Markdown (dạng chính) và JSON, cho người viết công cụ |
| [PROMPT.md](PROMPT.md) | Prompt cho AI (app soạn sẵn ở tab **Nhờ AI**) |
| `studypack-v1.schema.json` | JSON Schema của dạng JSON |
| `validate.ts` | Kiểm tra file: `node studypack/validate.ts <file.md \| file.zip \| file.json>` |
| `convert.ts` | Đổi JSON sang Markdown và ngược lại (`.md` hoặc `.zip` khi có ảnh) |
| `test-roundtrip.ts` | Kiểm thử: gói mẫu đổi sang Markdown rồi đổi ngược lại vẫn phải giữ nguyên từng câu |
| `examples/` | Quiz mẫu, bắt đầu từ `vi-du.md` |

Các công cụ chạy bằng Node 23.6+ (Node 22.6+ thì thêm `--experimental-strip-types`) và dùng chung code với app (`src/ui/src/lib/study/`), nên file nào qua được `validate.ts` thì app nhập được.

## An toàn

- Gói chỉ gồm chữ và ảnh; app không chạy code nào trong gói. HTML được lọc lại khi hiển thị (`sanitize.ts`).
- Ảnh chỉ nhận PNG, JPEG, WebP, GIF nhúng sẵn; không SVG, không link ngoài.
- Quiz LMS đã lưu chỉ xuất được sau khi quiz đóng.
