# Prompt cho AI

App soạn sẵn prompt: vào môn, chọn **Tạo**, chọn bài, rồi chọn tab **Nhờ AI**. Prompt đã điền sẵn môn, chương và bài đang chọn, nên kết quả nhập vào đúng chỗ. Hướng dẫn từng bước có ảnh: [Wiki: Luyện tập, mục 4](https://github.com/bk-study-desk/bk-study-desk/wiki/Luy%E1%BB%87n-t%E1%BA%ADp#4-nh%E1%BB%9D-ai-so%E1%BA%A1n-d%E1%BB%85-nh%E1%BA%A5t). Có 4 loại:

| Loại | Dùng khi |
|---|---|
| **Soạn mới** | có slide/đề, muốn thêm câu luyện tập |
| **Câu tương tự** | có một câu hay (kể cả câu trong quiz LMS đã lưu), muốn thêm câu cùng dạng, đổi số liệu |
| **Soát đáp án** | muốn AI kiểm lại các câu đang có, tìm câu sai |
| **Giải thích** | chưa hiểu một câu, nhờ AI giảng lại từng bước |

**Khung prompt** gồm các mục cố định để AI nào cũng làm theo được:

```
# VAI TRÒ      Bạn là trợ giảng đại học soạn câu luyện tập cho sinh viên.
# NHIỆM VỤ     <soạn mới / câu tương tự / soát đáp án>
# ĐẦU VÀO      Môn, chương, bài (giữ đúng tên), số câu, yêu cầu thêm, câu mẫu hoặc gói cần soát
# ĐẦU RA       Một khối ```markdown theo ĐỊNH DẠNG (Study Markdown)
# ĐỊNH DẠNG    Mẫu ngắn đủ 5 loại câu (xem SPEC.md mục 2)
# LUẬT         7 luật: chỉ một khối markdown, TeX không gấp đôi \, đủ đề/đáp án/lời giải,
               phương án nhiễu có lý, tự tính lại đáp án, đúng ký hiệu tài liệu, không lộ đề đang mở
# TỰ KIỂM      Checklist 6 dòng AI phải soát trước khi trả lời
# VÍ DỤ        Một câu mẫu đúng định dạng
```

Bản đầy đủ nằm trong `src/ui/src/lib/study/prompts.ts`. Sửa ở đó thì app đổi theo.

## Mẹo dùng

- **Gửi kèm tài liệu gốc** (PDF slide, ảnh đề) để AI bám đúng ký hiệu. Không có tài liệu thì AI dễ bịa.
- **Mỗi lần 5–10 câu.** Xin 30 câu một lần thì AI dễ sai đáp án hơn.
- **Bị báo lỗi khi nhập:** dán nguyên phần lỗi cho AI và bảo "sửa các lỗi này, trả lại toàn bộ khối markdown".
- **AI chạy được lệnh** (Claude Code, Codex...): bảo AI chạy `node studypack/validate.ts <file.md>` và tự sửa đến khi hết lỗi.
- **Bộ kiểm chỉ soát định dạng, không soát đáp án.** Luôn tự làm thử vài câu trước khi gửi cho người khác.
