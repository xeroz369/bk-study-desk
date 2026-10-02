// Chấm một câu theo loại (single, multi, numeric, short). Đúng/Sai được đổi thành single lúc nạp gói.
// Câu trả lời lưu trong kết quả luyện tập (ket-qua.json) và bài thi là giá trị JSON: số, mảng số hoặc chuỗi.

import type { Answer, Question } from './types';

export const qType = (q: Question) => q.type ?? 'single';

/** Chuẩn hóa chữ để so câu điền: bỏ khoảng trắng thừa, không phân biệt hoa thường; giữ dấu tiếng Việt. */
export const normText = (s: string) => s.normalize('NFC').toLowerCase().replace(/\s+/g, ' ').trim();

/** "1,5" (kiểu Việt) hay "1.5" đều đọc được; "1.234,5" cũng được. */
export function parseNumber(s: string): number | null {
	let t = s.trim().replace(/\s+/g, '');
	if (!t) return null;
	if (t.includes(',') && t.includes('.'))
		t = t.lastIndexOf(',') > t.lastIndexOf('.') ? t.replace(/\./g, '').replace(',', '.') : t.replace(/,/g, '');
	else t = t.replace(',', '.');
	const n = Number(t);
	return Number.isFinite(n) ? n : null;
}

export function isBlank(a: Answer | null | undefined) {
	return a === null || a === undefined || (Array.isArray(a) && a.length === 0) || (typeof a === 'string' && !a.trim());
}

export function isCorrect(q: Question, a: Answer | null | undefined): boolean {
	if (isBlank(a)) return false;
	switch (qType(q)) {
		case 'multi': {
			const want = [...(q.answers ?? [])].sort().join(',');
			return Array.isArray(a) && [...a].sort().join(',') === want;
		}
		case 'numeric': {
			const n = typeof a === 'number' ? a : parseNumber(String(a));
			if (n === null || q.value === undefined) return false;
			const tol = q.tolerance ?? 0;
			return Math.abs(n - q.value) <= tol + Math.abs(q.value) * 1e-12;
		}
		case 'short':
			return (q.accept ?? []).some((x) => normText(x) === normText(String(a)));
		default:
			return a === q.answer;
	}
}

/** Đáp án đúng dạng chữ (hiện sau khi làm, ở trang kết quả). */
export function answerText(q: Question): string {
	const L = 'ABCDEFGH';
	switch (qType(q)) {
		case 'multi':
			return (q.answers ?? []).map((i) => L[i]).join(', ');
		case 'numeric':
			return `${q.value}${q.tolerance ? ` ± ${q.tolerance}` : ''}${q.unit ? ' ' + q.unit : ''}`;
		case 'short':
			return (q.accept ?? []).join(' / ');
		default:
			return L[q.answer] ?? '';
	}
}
