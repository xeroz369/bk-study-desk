import { afterEach, describe, expect, it, vi } from 'vitest';
import { EXAM_SHUFFLE, loadPrefs, optionOrder, PRACTICE_SHUFFLE, rng, savePrefs, seedFor, shuffled } from './shuffle';
import type { Question } from './types';

const q = (options: string[], extra: Partial<Question> = {}): Question => ({
	id: 'q1',
	prompt: 'p',
	options,
	answer: 0,
	solution: '',
	...extra,
});

describe('rng, seedFor', () => {
	it('cùng seed thì cùng dãy, trong [0, 1)', () => {
		const a = rng(42),
			b = rng(42);
		const xs = Array.from({ length: 20 }, () => a());
		expect(Array.from({ length: 20 }, () => b())).toEqual(xs);
		expect(xs.every((x) => x >= 0 && x < 1)).toBe(true);
	});

	it('seedFor ổn định theo id, khác id thì khác seed', () => {
		expect(seedFor(7, 'a.q1')).toBe(seedFor(7, 'a.q1'));
		expect(seedFor(7, 'a.q1')).not.toBe(seedFor(7, 'a.q2'));
	});
});

describe('shuffled', () => {
	it('là một hoán vị, không đổi mảng gốc', () => {
		const src = [1, 2, 3, 4, 5, 6];
		const out = shuffled(src, rng(3));
		expect([...out].sort()).toEqual(src);
		expect(src).toEqual([1, 2, 3, 4, 5, 6]);
	});
});

describe('optionOrder', () => {
	it('phương án kiểu "Cả A và B", "Tất cả" đứng yên, phần còn lại được xáo', () => {
		const opts = ['x', 'y', 'z', 'Cả A và B đều đúng', 'Tất cả đều sai'];
		for (let s = 1; s < 20; s++) {
			const o = optionOrder(q(opts), rng(s));
			expect(o.slice(3)).toEqual([3, 4]);
			expect([...o].sort()).toEqual([0, 1, 2, 3, 4]);
		}
	});

	it('keepOrder và câu dưới 3 phương án thì giữ nguyên', () => {
		expect(optionOrder(q(['a', 'b', 'c', 'd'], { keepOrder: true }), rng(1))).toEqual([0, 1, 2, 3]);
		expect(optionOrder(q(['Đúng', 'Sai']), rng(1))).toEqual([0, 1]);
	});
});

describe('loadPrefs, savePrefs', () => {
	afterEach(() => vi.unstubAllGlobals());

	it('luyện tập và thi thử nhớ riêng', () => {
		const store = new Map<string, string>();
		vi.stubGlobal('localStorage', { getItem: (k: string) => store.get(k) ?? null, setItem: (k: string, v: string) => store.set(k, v) });
		const off = { questions: false, options: false };
		savePrefs({ questions: true, options: true }, EXAM_SHUFFLE);
		expect(loadPrefs(off, EXAM_SHUFFLE)).toEqual({ questions: true, options: true });
		expect(loadPrefs(off)).toEqual(off);
		savePrefs({ questions: true, options: false });
		expect(store.has(PRACTICE_SHUFFLE)).toBe(true);
	});

	it('storage bị chặn hoặc hỏng thì dùng mặc định', () => {
		vi.stubGlobal('localStorage', {
			getItem: () => '{hỏng',
			setItem: () => {
				throw new Error('blocked');
			},
		});
		const d = { questions: true, options: false };
		expect(loadPrefs(d)).toEqual(d);
		expect(() => savePrefs(d)).not.toThrow();
	});
});
