# studypack: your own quizzes

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](README.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](README.en.md)

> Translated from README.md (Vietnamese) for BK Study Desk 1.1.3.

Write quizzes to revise and share with friends: type them in the app, ask AI, or convert to and from Moodle (BK-LMS).

| File | Used for |
|---|---|
| [Wiki: Practice](https://github.com/xeroz369/bk-study-desk/wiki/Practice) | **Start here**: step-by-step guide with screenshots, no programming needed |
| [SPEC.en.md](SPEC.en.md) | Full specification of Study Markdown (the main format) and JSON, for tool authors |
| [PROMPT.md](PROMPT.md) | AI prompt (Vietnamese; the app fills it in on the **Nhờ AI** (Ask AI) tab) |
| `studypack-v1.schema.json` | JSON Schema of the JSON format |
| `validate.ts` | Check a file: `node studypack/validate.ts <file.md \| file.zip \| file.json>` |
| `convert.ts` | Convert JSON ↔ Markdown (`.md`, or `.zip` with images) |
| `test-roundtrip.ts` | Test: the sample packs converted to Markdown and back must keep every question unchanged |
| `examples/` | Sample quizzes; start with `vi-du.md` |

The tools run on Node 23.6+ (on Node 22.6+ add `--experimental-strip-types`) and share code with the app (`src/ui/src/lib/study/`), so any file that passes `validate.ts` can be imported by the app.

## Safety

- A pack contains only text and images; the app runs no code from it. HTML is sanitized again when displayed (`sanitize.ts`).
- Images must be embedded PNG, JPEG, WebP or GIF; no SVG, no external links.
- Saved LMS quizzes can be exported only after the quiz has closed.
