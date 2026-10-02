// Prompts for any AI (ChatGPT, Claude, Gemini…). Every prompt has the same fixed sections so small models follow it:
//   # VAI TRÒ · # NHIỆM VỤ · # ĐẦU VÀO · # ĐẦU RA · # ĐỊNH DẠNG · # LUẬT · # TỰ KIỂM · # VÍ DỤ
// Output is always Study Markdown (studypack/SPEC.md): no JSON escaping, so AIs make fewer mistakes.

export type PromptMode = 'soan' | 'tuong-tu' | 'soat' | 'giai-thich';

export interface PromptInput {
	course: string; // "MT1009 Phương pháp tính"
	unit?: string; // chapter title (keeps the result in the right place)
	lesson?: string; // lesson title
	units?: string[]; // existing chapter titles (course scope)
	count: number;
	extra: string;
	question?: string; // one question in Study Markdown (similar / explain)
	pack?: string; // a whole pack in Study Markdown (review)
}

export const FORMAT = `---
mon: <MÃ MÔN> <Tên môn>
tac-gia: <tên người tạo>
tao-boi: <tên AI và model của bạn>
da-kiem: <bạn đã kiểm đáp án bằng cách nào>
---

# <Tên chương>

## <Tên bài>

<Kiến thức ngắn của bài, Markdown + TeX. Có thể bỏ trống.>

### Câu 1 · <nguồn ngắn, vd. Slide tr.36>
<Đề bài>
- [ ] <phương án sai>
- [x] <phương án đúng>
- [ ] <phương án sai>
- [ ] <phương án sai>
> <Lời giải: công thức, từng bước, số trung gian>

### Câu 2
<Đề có NHIỀU đáp án đúng: tick nhiều ô>
- [x] <đúng>
- [ ] <sai>
- [x] <đúng>
> <Lời giải>

### Câu 3
<Đề điền số>
= <đáp số> ± <sai số cho phép> (<đơn vị, nếu có>)
> <Lời giải>

### Câu 4
<Phát biểu để chọn Đúng/Sai>
= Đúng
> <Lời giải>

### Câu 5
<Đề điền chữ>
= <đáp án> | <cách viết khác cũng đúng>
> <Lời giải>`;

const RULES = `1. Chỉ trả về MỘT khối \`\`\`markdown ... \`\`\`, không viết gì ngoài khối đó.
2. Công thức viết TeX: \\( ... \\) trong dòng, \\[ ... \\] riêng dòng. Viết TeX bình thường (\\frac, \\sqrt), KHÔNG gấp đôi dấu \\.
3. Mỗi câu phải có: đề, đáp án (các dòng "- [x]/- [ ]" hoặc một dòng "= ..."), và lời giải (dòng bắt đầu bằng ">").
4. Trắc nghiệm: 4 phương án; phương án nhiễu là lỗi sinh viên hay mắc (sai dấu, lệch một bước, quên đổi radian, nhầm công thức), không phải số ngẫu nhiên.
5. Câu tính toán: TỰ TÍNH LẠI đáp án (chạy code nếu được) rồi mới ghi. Không chắc thì bỏ câu đó.
6. Dùng đúng ký hiệu, tên phương pháp của tài liệu được gửi kèm. Không bịa kiến thức ngoài tài liệu.
7. Không đưa đáp án bài kiểm tra/quiz đang mở. Đề cũ thì ghi nguồn sau dấu "·" ở dòng ###.`;

const CHECK = `- [ ] Chỉ có một khối markdown, bắt đầu bằng phần --- có dòng "mon:".
- [ ] Tên chương (#) và tên bài (##) đúng như ĐẦU VÀO.
- [ ] Mỗi câu (###) có đề, đáp án, lời giải ">".
- [ ] Mỗi câu trắc nghiệm có ít nhất một "- [x]".
- [ ] Đã tự tính lại đáp án các câu tính toán.
- [ ] Không có HTML lạ, không có link ảnh ngoài.`;

const EXAMPLE = `### Câu 1 · Slide tr.36
\\(f(x)=x^3-2\\) trên \\([1,2]\\). Theo phương pháp chia đôi, \\(x_2\\) là
- [ ] 1.125
- [ ] 1.25
- [x] 1.375
- [ ] 1.5
> \\(x_0=1.5\\): \\(f(1.5)=1.375>0\\). \\(x_1=1.25\\): \\(f(1.25)<0\\). Vậy \\(x_2=\\frac{1.25+1.5}{2}=1.375\\).`;

function where(p: PromptInput) {
	if (p.lesson) return `Chương "${p.unit}", bài "${p.lesson}". Giữ ĐÚNG hai tên này ở dòng # và ##.`;
	if (p.unit) return `Chương "${p.unit}" (giữ đúng tên ở dòng #). Tự đặt tên bài (##) theo nội dung.`;
	return `Các chương đang có: ${(p.units ?? []).map((u) => `"${u}"`).join(', ') || 'chưa có'}. Nội dung thuộc chương nào thì dùng đúng tên chương đó.`;
}

export function buildPrompt(mode: PromptMode, p: PromptInput): string {
	const task = {
		soan: `Tạo khoảng ${p.count} câu luyện tập mới (kèm kiến thức ngắn nếu là bài mới) từ tài liệu tôi gửi kèm.`,
		'tuong-tu': `Tạo ${p.count} câu TƯƠNG TỰ câu mẫu bên dưới: cùng dạng bài và mức khó, đổi số liệu/ngữ cảnh, tính lại đáp án.`,
		soat: 'Soát lại gói câu hỏi bên dưới: tính lại từng đáp án, sửa câu sai, lời giải thiếu bước. Trả về TOÀN BỘ gói đã sửa; cuối mỗi câu đã sửa thêm dòng "> Đã sửa: <lý do>".',
		'giai-thich':
			'Giải thích câu bên dưới cho sinh viên chưa hiểu: nhắc lại kiến thức cần dùng, giải từng bước, chỉ ra vì sao các phương án sai là sai, và một mẹo nhớ. Trả lời bằng chữ thường (Markdown), KHÔNG cần theo ĐỊNH DẠNG gói.',
	}[mode];
	const input = [
		`Môn: ${p.course}.`,
		where(p),
		p.extra.trim() ? `Yêu cầu thêm: ${p.extra.trim()}` : '',
		mode === 'soan' ? 'Tài liệu: slide/đề tôi gửi kèm (PDF, ảnh).' : '',
		p.question ? `Câu mẫu:\n\`\`\`markdown\n${p.question}\n\`\`\`` : '',
		p.pack ? `Gói cần soát:\n\`\`\`markdown\n${p.pack}\n\`\`\`` : '',
	]
		.filter(Boolean)
		.join('\n');
	if (mode === 'giai-thich')
		return `# VAI TRÒ\nBạn là trợ giảng đại học, giải thích rõ ràng, ngắn gọn bằng tiếng Việt.\n\n# NHIỆM VỤ\n${task}\n\n# ĐẦU VÀO\n${input}\n\n# LUẬT\nCông thức viết TeX \\( ... \\). Dùng đúng ký hiệu trong câu. Không dài quá 300 chữ.`;
	return `# VAI TRÒ
Bạn là trợ giảng đại học tạo câu luyện tập cho sinh viên.

# NHIỆM VỤ
${task}

# ĐẦU VÀO
${input}

# ĐẦU RA
Một khối \`\`\`markdown theo ĐỊNH DẠNG bên dưới (Study Markdown). Người dùng sẽ dán nguyên khối này vào app.

# ĐỊNH DẠNG
${FORMAT}

# LUẬT
${RULES}

# TỰ KIỂM (soát trước khi trả lời)
${CHECK}

# VÍ DỤ (một câu đúng định dạng)
${EXAMPLE}`;
}
