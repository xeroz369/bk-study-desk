import { describe, expect, it } from 'vitest';
import { canMix, interleave } from './interleave';
import { rng } from './shuffle';
import type { Question } from './types';

const q = (id: string, extra: Partial<Question> = {}): Question => ({
	id,
	lessonId: id.split('.').slice(0, 2).join('.'),
	fp: 'fp-' + id,
	prompt: id,
	options: ['a', 'b'],
	answer: 0,
	solution: '',
	...extra,
});
const lesson = (u: number, l: number, n: number, extra: (i: number) => Partial<Question> = () => ({})) =>
	Array.from({ length: n }, (_, i) => q(`${u}.${l}.${i + 1}`, extra(i)));
const unitOf = (x: Question) => x.id.split('.')[0];

describe('interleave', () => {
	it('xen kẽ chương: hai câu liền nhau không cùng chương khi còn chương khác', () => {
		const units = [[lesson(0, 0, 6)], [lesson(1, 0, 6)], [lesson(2, 0, 2)]];
		for (let seed = 1; seed < 20; seed++) {
			const out = interleave(units, 12, () => 0, rng(seed));
			expect(out).toHaveLength(12);
			const u = out.map(unitOf);
			// 2 câu chương 2 hết trong 3 vòng đầu; sau đó chỉ còn chương 0 và 1, vẫn xen nhau.
			for (let i = 1; i < u.length; i++) expect(u[i]).not.toBe(u[i - 1]);
		}
	});

	it('không vượt số câu, mỗi fingerprint một lần', () => {
		const units = [[lesson(0, 0, 3)], [lesson(1, 0, 3, (i) => (i === 0 ? { fp: 'fp-0.0.1' } : {}))]];
		const out = interleave(units, 50, () => 0, rng(3));
		expect(new Set(out.map((x) => x.fp)).size).toBe(out.length);
		expect(out).toHaveLength(5);
	});

	it('câu cùng nhóm đi liền nhau, không bị cắt', () => {
		const units = [[lesson(0, 0, 3, (i) => (i < 2 ? { group: 'g' } : {}))], [lesson(1, 0, 3)]];
		for (let seed = 1; seed < 20; seed++) {
			const ids = interleave(units, 6, () => 0, rng(seed)).map((x) => x.id);
			expect(Math.abs(ids.indexOf('0.0.1') - ids.indexOf('0.0.2'))).toBe(1);
		}
	});

	it('câu có rank nhỏ (chưa làm, tới hạn ôn) lên trước trong từng chương', () => {
		const units = [[lesson(0, 0, 4)], [lesson(1, 0, 4)]];
		const rank = (x: Question) => (x.id.endsWith('.4') ? 0 : 1);
		const out = interleave(units, 2, rank, rng(7)).map((x) => x.id);
		expect(out.sort()).toEqual(['0.0.4', '1.0.4']);
	});
});

describe('canMix', () => {
	it('cần ít nhất hai chương có câu', () => {
		expect(canMix([[lesson(0, 0, 5)], [[]]])).toBe(false);
		expect(canMix([[lesson(0, 0, 1)], [lesson(1, 0, 1)]])).toBe(true);
	});
});
