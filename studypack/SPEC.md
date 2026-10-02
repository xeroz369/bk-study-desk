# Study Pack v1: định dạng gói luyện tập

Bản ngắn cho người dùng: [HUONG-DAN.md](HUONG-DAN.md). File này là đặc tả đầy đủ cho người viết công cụ và cho AI.

## 1. Ba dạng của cùng một gói

| Dạng | Dùng khi | Ghi chú |
|---|---|---|
| **Study Markdown** `.md` | soạn tay, AI soạn, chia sẻ | **dạng chính**: đọc bằng mắt được, không phải escape |
| **Gói Markdown có ảnh** `.zip` | gói có ảnh | gồm `goi.md` (hoặc một file `.md` bất kỳ) và thư mục `img/` |
| **JSON** `.studypack.json` | lưu trong app, công cụ | app tự đổi qua lại; schema: `studypack-v1.schema.json` |

Ba dạng chuyển qua lại không mất gì. Công cụ: `node studypack/convert.ts`. Kiểm thử khứ hồi: `node studypack/test-roundtrip.ts`.

## 2. Study Markdown

### 2.1 Phần đầu (front matter)

Nằm giữa hai dòng `---` ở đầu file, mỗi dòng có dạng `khóa: giá trị`.

| Khóa | Bắt buộc | Ý nghĩa | Ví dụ |
|---|---|---|---|
| `mon` | có* | mã môn + tên môn | `MT1009 Phương pháp tính` |
| `tac-gia` | nên có | tác giả, ngăn bằng dấu phẩy | `An, Bình` |
| `tieu-de` | | tên gói (mặc định: tên chương đầu) | `Chia đôi và lặp đơn` |
| `id` | | id gói (mặc định tạo từ trường + mã môn + tên) | `hcmut-mt1009-chia-doi` |
| `truong` | | trường | `HCMUT` |
| `phien-ban` | | `x.y.z`, mặc định `1.0.0` | `1.1.0` |
| `giay-phep` | nên có | giấy phép | `CC-BY-SA-4.0` |
| `tao-boi` | | ai soạn (người, AI nào) | `ChatGPT-5 + người kiểm` |
| `da-kiem` | nên có | đã kiểm đáp án bằng cách nào | `tính lại bằng Python` |
| `xao-cau` | | xáo thứ tự câu khi làm (`co` / `khong`) | `co` |
| `xao-dap-an` | | xáo thứ tự phương án | `co` |
| `nguon` | | tài liệu gốc, ngăn bằng `;` | `Slide chương 1; GK251` |

\* Thiếu `mon` thì app dùng môn đang chọn lúc nhập. Khóa tiếng Anh (`course`, `authors`, `title`, `shuffle-questions`…) cũng được.

### 2.2 Cấu trúc

```
# Chương            → unit; trùng tên chương có sẵn thì ghép vào đó
## Bài              → lesson; trùng tên bài trong chương thì thêm câu vào bài đó
   (chữ ngay dưới)  → kiến thức của bài; "#### Tiêu đề" chia thành nhiều mục
### Câu …           → câu hỏi
# Đề thi thử        → (tên dành riêng) các ## bên dưới là đề thi thử
## Tên đề           → dòng kế: "Thời gian: 50 phút · Đúng: 0.588 · Sai: -0.118"
### Câu …           → câu của đề
```

### 2.3 Câu hỏi

```
### Câu <số> · <nguồn> · giữ thứ tự        (nguồn và "giữ thứ tự" không bắt buộc)
<đề: Markdown + TeX, nhiều dòng, có thể có ảnh>
<đáp án: một trong các dạng ở bảng dưới>
> <lời giải, mỗi dòng bắt đầu bằng ">">
```

| Loại | Đáp án viết | Tương ứng Moodle |
|---|---|---|
| Một đáp án | các dòng `- [ ] …`, đúng một dòng `- [x] …` | multichoice (single) |
| Nhiều đáp án | như trên, nhiều dòng `- [x]` | multichoice (multiple) |
| Đúng/Sai | `= Đúng` hoặc `= Sai` (`True`/`False` cũng được) | truefalse |
| Điền số | `= 0,125`, `= 0.125 ± 0.001`, `= 9,8 ± 0,1 (m/s²)` | numerical |
| Điền chữ | `= Newton \| Newton-Raphson` (không phân biệt hoa thường, khoảng trắng) | shortanswer |

- Phương án nằm trên **một dòng**, có thể kèm ảnh `![](img/a.webp)`.
- Chỉ dòng `###` bắt đầu một câu mới; dòng trống bên trong câu không làm tách câu.
- `· giữ thứ tự`: không xáo phương án của câu này. Các phương án kiểu "Tất cả…", "Các đáp án khác…", "Cả A và B…" vốn đã luôn đứng yên.

### 2.4 Nội dung

- **Markdown:** đoạn văn, `**đậm**`, `*nghiêng*`, `` `mã` ``, danh sách `-` và `1.`, bảng `| a | b |`, ảnh `![mô tả](img/x.webp)`.
- **TeX:** `\( … \)` trong dòng, `\[ … \]` hoặc `$$ … $$` riêng dòng. Viết TeX bình thường, không gấp đôi `\`.
- **HTML đơn giản** cũng được, ví dụ `<div class="warn">Lưu ý…</div>`:
  - class hợp lệ: `math`, `warn`, `keys` (bấm máy), `tip`, `note`;
  - app lọc bỏ mọi thứ khác (script, thuộc tính sự kiện…).
- **Dấu `<` trong công thức** nên viết `\lt`, vì `x<y` có thể bị hiểu nhầm là thẻ HTML.

### 2.5 Ảnh

- Chỉ nhận PNG, JPEG, WebP, GIF. Không nhận SVG, vì SVG có thể chứa script.
- Không nhận link ngoài (`https://…`): gói phải dùng được offline, và link ngoài có thể dùng để theo dõi người mở.
- Trong `.zip`, ảnh nằm ở `img/` và được tham chiếu đúng đường dẫn đó. Thiếu ảnh chỉ là cảnh báo: chỗ ảnh hiện "[thiếu ảnh]".
- App tự nén ảnh khi soạn: cạnh dài tối đa 1200 px, WebP. Mỗi ảnh nên dưới 1 MB; cả gói tối đa 20 MB.

## 3. Luật nội dung

1. Mỗi câu có đề, đáp án và **lời giải đủ để tự làm lại**.
2. Câu tính toán: đáp án phải **được tính lại** (Python, máy tính). Ghi cách kiểm vào `da-kiem`.
3. Phương án nhiễu là lỗi sinh viên hay mắc, không phải số ngẫu nhiên.
4. Dùng đúng ký hiệu và tên phương pháp như tài liệu của môn; ghi nguồn sau dấu `·`.
5. **Không** đưa đáp án bài kiểm tra/quiz đang mở. Quiz LMS đã lưu chỉ chia sẻ khi quiz đã đóng (app tự khóa).

## 4. Tương thích

| Định dạng | Nhập | Xuất | Ghi chú |
|---|---|---|---|
| Moodle XML | ✓ | ✓ | category → bài; ảnh `@@PLUGINFILE@@` ↔ `img/`. Giảng viên nhập file vào Ngân hàng câu hỏi trên LMS |
| GIFT | ✓ | ✓ | đủ 5 loại; không mang ảnh |
| Aiken | ✓ | | chỉ một đáp án, không lời giải |
| JSON (`studypack/1`) | ✓ | ✓ | dạng lưu trong app |

**Bỏ qua kèm thông báo:** câu ghép cặp, tự luận, cloze, kéo thả, câu có biến số. Dự kiến hỗ trợ ở v2.

## 5. Kiểm tra và phiên bản

- `node studypack/validate.ts <file.md|file.zip|file.json>`: cùng bộ kiểm với app, báo lỗi theo số dòng. Exit code 1 nếu có lỗi, nên AI agent tự sửa được.
- Định dạng giữ tên `studypack/1` và chỉ **thêm**, không bỏ khóa cũ, nên gói cũ luôn mở được. Thay đổi phá vỡ tương thích sẽ dùng `studypack/2`.
- Sửa gói thì tăng `phien-ban`: sửa lỗi tăng số cuối, thêm câu tăng số giữa.
