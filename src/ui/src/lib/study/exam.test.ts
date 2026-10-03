import { describe, expect, it } from 'vitest';
import { blocks, enoughForRandomExam, pickRandomExam, shuffleGroups, spread } from './exam';
import { rng } from './shuffle';
import type { Question } from './types';

/** Câu giả: id "u.l.q", fp duy nhất trừ khi truyền fp. */
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
const ids = (qs: Question[]) => qs.map((x) => x.id);
const lesson = (u: number, l: number, n: number, extra: (i: number) => Partial<Question> = () => ({})) =>
	Array.from({ length: n }, (_, i) => q(`${u}.${l}.${i + 1}`, extra(i)));

describe('spread', () => {
	it('chia theo cỡ, không vượt số câu có', () => {
		expect(spread(10, [10, 10])).toEqual([5, 5]);
		expect(spread(10, [3, 30])).toEqual([1, 9]);
		expect(spread(50, [3, 4])).toEqual([3, 4]);
		expect(spread(5, [0, 0])).toEqual([0, 0]);
	});
});

describe('pickRandomExam', () => {
	it('giữ thứ tự gốc: chương, bài, câu', () => {
		const units = [[lesson(0, 0, 6), lesson(0, 1, 6)], [lesson(1, 0, 8)]];
		for (let seed = 1; seed < 30; seed++) {
			const { questions } = pickRandomExam(units, undefined, 10, () => 0, rng(seed));
			expect(questions).toHaveLength(10);
			const order = units.flat(2).map((x) => x.id);
			const at = ids(questions).map((id) => order.indexOf(id));
			expect(at).toEqual([...at].sort((a, b) => a - b));
		}
	});

	it('nhóm được rút cả nhóm hoặc bỏ cả nhóm, đứng liền nhau', () => {
		// Bài có câu 2-4 cùng nhóm "f": dùng chung đề.
		const l = lesson(0, 0, 8, (i) => (i >= 1 && i <= 3 ? { group: 'f' } : {}));
		for (let seed = 1; seed < 50; seed++) {
			const { questions } = pickRandomExam([[l]], undefined, 5, () => 0, rng(seed));
			const got = ids(questions);
			const g = got.filter((id) => ['0.0.2', '0.0.3', '0.0.4'].includes(id));
			expect([0, 3]).toContain(g.length);
			if (g.length) {
				const i = got.indexOf('0.0.2');
				expect(got.slice(i, i + 3)).toEqual(['0.0.2', '0.0.3', '0.0.4']);
			}
			expect(got.length).toBeLessThanOrEqual(5);
		}
	});

	it('cùng tên nhóm ở hai bài khác nhau là hai nhóm', () => {
		const a = lesson(0, 0, 2, () => ({ group: 'g' }));
		const b = lesson(0, 1, 2, () => ({ group: 'g' }));
		const { questions } = pickRandomExam([[a, b]], undefined, 2, () => 0, rng(3));
		expect(questions).toHaveLength(2);
		expect(new Set(questions.map((x) => x.lessonId)).size).toBe(1);
	});

	it('mỗi fingerprint chỉ một lần', () => {
		const a = lesson(0, 0, 3);
		const b = [q('0.1.1', { fp: a[0].fp }), q('0.1.2')];
		const { questions } = pickRandomExam([[a, b]], undefined, 10, () => 0, rng(1));
		expect(ids(questions)).toEqual(['0.0.1', '0.0.2', '0.0.3', '0.1.2']);
	});

	it('rank nhỏ được rút trước', () => {
		const l = lesson(0, 0, 10);
		const { questions } = pickRandomExam([[l]], undefined, 3, (x) => (['0.0.7', '0.0.8', '0.0.9'].includes(x.id) ? 0 : 1), rng(5));
		expect(ids(questions)).toEqual(['0.0.7', '0.0.8', '0.0.9']);
	});

	it('theo blueprint: số câu mỗi chương, chương 0 câu không được bù', () => {
		const units = [[lesson(0, 0, 10)], [lesson(1, 0, 10)], [lesson(2, 0, 10)]];
		const { questions, total } = pickRandomExam(units, { minutes: 30, units: [3, 2, 0] }, 20, () => 0, rng(2));
		expect(total).toBe(5);
		expect(questions.filter((x) => x.id.startsWith('0.'))).toHaveLength(3);
		expect(questions.filter((x) => x.id.startsWith('1.'))).toHaveLength(2);
		expect(questions.filter((x) => x.id.startsWith('2.'))).toHaveLength(0);
	});

	it('chương thiếu câu nhường phần cho chương khác', () => {
		const units = [[lesson(0, 0, 1)], [lesson(1, 0, 10)]];
		const { questions } = pickRandomExam(units, { minutes: 30, units: [4, 4] }, 20, () => 0, rng(2));
		expect(questions).toHaveLength(8);
		expect(questions[0].id).toBe('0.0.1');
	});
});

describe('shuffleGroups', () => {
	it('xáo khối nhưng giữ nhóm liền nhau và đúng thứ tự', () => {
		const qs = lesson(0, 0, 10, (i) => (i >= 4 && i <= 6 ? { group: 'x' } : {}));
		let moved = false;
		for (let seed = 1; seed < 40; seed++) {
			const out = ids(shuffleGroups(qs, rng(seed)));
			expect([...out].sort()).toEqual([...ids(qs)].sort());
			const i = out.indexOf('0.0.5');
			expect(out.slice(i, i + 3)).toEqual(['0.0.5', '0.0.6', '0.0.7']);
			if (out.join() !== ids(qs).join()) moved = true;
		}
		expect(moved).toBe(true);
	});

	it('blocks: câu lẻ mỗi câu một khối, nhóm ở vị trí câu đầu', () => {
		const qs = [q('0.0.1', { group: 'a' }), q('0.0.2'), q('0.0.3', { group: 'a' })];
		expect(blocks(qs).map(ids)).toEqual([['0.0.1', '0.0.3'], ['0.0.2']]);
	});
});

describe('enoughForRandomExam', () => {
	it('10 câu, hoặc có kiểu đề thật và có câu', () => {
		expect(enoughForRandomExam(10, false)).toBe(true);
		expect(enoughForRandomExam(9, false)).toBe(false);
		expect(enoughForRandomExam(1, true)).toBe(true);
		expect(enoughForRandomExam(0, true)).toBe(false);
	});
});
