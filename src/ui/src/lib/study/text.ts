/**
 * Chữ thuần từ một đoạn HTML, chỉ để so sánh, kiểm tra rỗng hay đọc số (không dùng để hiển thị; hiển thị đi qua sanitize.ts).
 * Bỏ thẻ lặp lại đến khi hết (chuỗi kiểu "<scr<script>ipt>" không còn sót thẻ), rồi bỏ mọi dấu < > còn lại.
 */
export function plainText(html: string): string {
	let s = html;
	let prev: string;
	do {
		prev = s;
		s = s.replace(/<[^<>]*>/g, '');
	} while (s !== prev);
	return s.replace(/[<>]/g, '');
}

/**
 * Chuẩn hóa chữ để so sánh: NFC, chữ thường, gộp khoảng trắng. Giữ dấu tiếng Việt.
 * Dùng chung cho câu điền (grade), tên chương/bài khi ghép gói (registry, transfer) và nhãn đáp án LMS (lmsquiz).
 */
export const normText = (s: string) => s.normalize('NFC').toLowerCase().replace(/\s+/g, ' ').trim();
