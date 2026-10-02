# Study Pack v1: practice pack format

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](SPEC.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](SPEC.en.md)

> Translated from SPEC.md (Vietnamese) for BK Study Desk 1.1.3.

User guide: [Wiki > Practice](https://github.com/xeroz369/bk-study-desk/wiki/Practice). This file is the full specification for tool authors and for AI.

The format's keywords are Vietnamese (for example `mon`, `### Câu`, `= Đúng`), because its users are Vietnamese students. English keys are accepted where noted.

## 1. Three forms of the same pack

| Form | Use it for | Notes |
|---|---|---|
| **Study Markdown** `.md` | writing by hand, AI writing, sharing | **main form**: readable as is, no escaping |
| **Markdown pack with images** `.zip` | packs with images | contains `goi.md` (or any single `.md` file) and an `img/` folder |
| **JSON** `.studypack.json` | storage in the app, tools | the app converts back and forth; schema: `studypack-v1.schema.json` |

The three forms convert into each other without loss. Tool: `node studypack/convert.ts`. Round-trip test: `node studypack/test-roundtrip.ts`.

## 2. Study Markdown

### 2.1 Front matter

Between two `---` lines at the top of the file, one `key: value` per line.

| Key | Required | Meaning | Example |
|---|---|---|---|
| `mon` | yes* | course code + course name | `MT1009 Phương pháp tính` |
| `tac-gia` | recommended | authors, comma-separated | `An, Bình` |
| `tieu-de` | | pack title (default: first chapter name) | `Chia đôi và lặp đơn` |
| `id` | | pack id (default: built from school + course code + title) | `hcmut-mt1009-chia-doi` |
| `truong` | | school | `HCMUT` |
| `phien-ban` | | `x.y.z`, default `1.0.0` | `1.1.0` |
| `giay-phep` | recommended | license | `CC-BY-SA-4.0` |
| `tao-boi` | | who wrote it (people, which AI) | `ChatGPT-5 + human check` |
| `da-kiem` | recommended | how the answers were checked | `recomputed in Python` |
| `xao-cau` | | shuffle question order (`co` = yes / `khong` = no) | `co` |
| `xao-dap-an` | | shuffle answer options | `co` |
| `nguon` | | sources, separated by `;` | `Slide chương 1; GK251` |

\* Without `mon`, the app uses the course selected at import time. English keys (`course`, `authors`, `title`, `shuffle-questions`...) also work.

### 2.2 Structure

```
# Chapter           : unit; if a chapter with that name exists, merge into it
## Lesson           : lesson; if the lesson exists in that chapter, add questions to it
   (text below it)  : the lesson's key points; "#### Heading" splits them into sections
### Câu ...           : a question
# Đề thi thử        : (reserved name: "mock exams") each ## below it is a mock exam
## Exam name        : next line: "Thời gian: 50 phút · Đúng: 0.588 · Sai: -0.118" (time · points for right · points for wrong)
### Câu ...           : a question in that exam
```

### 2.3 Questions

```
### Câu <number> · <source> · giữ thứ tự      (source and "giữ thứ tự" = keep order are optional)
<question: Markdown + TeX, several lines, may include images>
<answer: one of the forms in the table below>
> <solution, every line starts with ">">
```

| Type | How to write the answer | Moodle equivalent |
|---|---|---|
| Single answer | `- [ ] ...` lines, exactly one `- [x] ...` | multichoice (single) |
| Multiple answers | as above, several `- [x]` lines | multichoice (multiple) |
| True/false | `= Đúng` or `= Sai` (`True`/`False` also work) | truefalse |
| Numeric | `= 0,125`, `= 0.125 ± 0.001`, `= 9,8 ± 0,1 (m/s²)` | numerical |
| Short text | `= Newton \| Newton-Raphson` (case and spaces ignored) | shortanswer |

- Each option is on **one line** and may include an image `![](img/a.webp)`.
- Only a `###` line starts a new question; blank lines inside a question don't split it.
- `· giữ thứ tự` (keep order): don't shuffle this question's options. Options like "Tất cả..." (All...), "Các đáp án khác..." (None of the others...), "Cả A và B..." (Both A and B...) always stay in place anyway.

### 2.4 Content

- **Markdown:** paragraphs, `**bold**`, `*italic*`, `` `code` ``, `-` and `1.` lists, tables `| a | b |`, images `![description](img/x.webp)`.
- **TeX:** `\( ... \)` inline, `\[ ... \]` or `$$ ... $$` on its own line. Write normal TeX; don't double the `\`.
- **Simple HTML** also works, for example `<div class="warn">Note...</div>`:
  - allowed classes: `math`, `warn`, `keys` (calculator keystrokes), `tip`, `note`;
  - the app strips everything else (scripts, event attributes...).
- **A `<` in a formula** should be written `\lt`, because `x<y` can be mistaken for an HTML tag.

### 2.5 Images

- Only PNG, JPEG, WebP and GIF. No SVG, because SVG can contain scripts.
- No external links (`https://...`): a pack must work offline, and external links can be used to track who opens it.
- In a `.zip`, images live in `img/` and are referenced by exactly that path. A missing image is only a warning: its place shows "[thiếu ảnh]" (missing image).
- The app compresses images while you write: longest side at most 1,200 px, WebP. Keep each image under 1 MB; a whole pack can be at most 20 MB.

## 3. Content rules

1. Every question has a question, an answer and **a solution complete enough to redo it**.
2. For calculations, the answer must be **recomputed** (Python, a calculator). Write how it was checked in `da-kiem`.
3. Wrong options are mistakes students really make, not random numbers.
4. Use the same notation and method names as the course materials; put the source after `·`.
5. **Don't** include answers to tests or quizzes that are still open. Saved LMS quizzes can be shared only after the quiz has closed (the app enforces this).

## 4. Compatibility

| Format | Import | Export | Notes |
|---|---|---|---|
| Moodle XML | yes | yes | category becomes lesson; `@@PLUGINFILE@@` images convert to and from `img/`. Lecturers import the file into the LMS question bank |
| GIFT | yes | yes | all 5 types; no images |
| Aiken | yes | | single answer only, no solutions |
| JSON (`studypack/1`) | yes | yes | the app's storage format |

**Skipped with a message:** matching, essay, cloze, drag-and-drop, and calculated questions. Planned for v2.

## 5. Validation and versions

- `node studypack/validate.ts <file.md|file.zip|file.json>`: the same checks as the app, errors reported by line number. Exits with code 1 on errors, so an AI agent can fix them by itself.
- The format keeps the name `studypack/1` and only **adds** keys, never removes them, so old packs always open. A breaking change will use `studypack/2`.
- When you edit a pack, bump `phien-ban`: a fix bumps the last number, new questions bump the middle one.
