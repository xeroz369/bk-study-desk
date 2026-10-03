import { describe, expect, it } from 'vitest';
import { answerText, isBlank, isCorrect, parseNumber } from './grade';
import type { Question } from './types';

const base: Question = { id: 'q', prompt: 'p', options: ['a', 'b', 'c'], answer: 1, solution: '' };

describe('parseNumber', () => {
	it.each([
		['1,5', 1.5],
		['1.5', 1.5],
		[' -2 ', -2],
		['1.234,5', 1234.5],
		['1,234.5', 1234.5],
		['1 000', 1000],
		['1e-3', 0.001],
	])('%s -> %d', (s, n) => expect(parseNumber(s)).toBe(n));

	it.each(['', '  ', 'abc', '1,2,3x'])('"%s" không phải số', (s) => expect(parseNumber(s)).toBeNull());
});

describe('isBlank', () => {
	it('null, undefined, mảng rỗng, chuỗi trắng là bỏ trống; số 0 thì không', () => {
		expect([null, undefined, [], '  '].every((a) => isBlank(a))).toBe(true);
		expect(isBlank(0)).toBe(false);
		expect(isBlank([0])).toBe(false);
	});
});

describe('isCorrect', () => {
	it('single: đúng chỉ số', () => {
		expect(isCorrect(base, 1)).toBe(true);
		expect(isCorrect(base, 0)).toBe(false);
		expect(isCorrect(base, null)).toBe(false);
	});

	it('multi: đủ và đúng các chỉ số, không phụ thuộc thứ tự chọn', () => {
		const q: Question = { ...base, type: 'multi', answer: -1, answers: [0, 2] };
		expect(isCorrect(q, [2, 0])).toBe(true);
		expect(isCorrect(q, [0])).toBe(false);
		expect(isCorrect(q, [0, 1, 2])).toBe(false);
	});

	it('numeric: theo sai số, nhận dấu phẩy kiểu Việt', () => {
		const q: Question = { ...base, type: 'numeric', options: [], answer: -1, value: 1.414, tolerance: 0.001 };
		expect(isCorrect(q, '1,4145')).toBe(true);
		expect(isCorrect(q, 1.413)).toBe(true);
		expect(isCorrect(q, '1.42')).toBe(false);
		expect(isCorrect(q, 'abc')).toBe(false);
		expect(isCorrect({ ...q, tolerance: undefined, value: 0.1 + 0.2 }, '0.3')).toBe(true);
	});

	it('short: không phân biệt hoa thường, khoảng trắng; giữ dấu', () => {
		const q: Question = { ...base, type: 'short', options: [], answer: -1, accept: ['Newton-Raphson', 'Tiếp tuyến'] };
		expect(isCorrect(q, '  newton-raphson ')).toBe(true);
		expect(isCorrect(q, 'TIẾP   TUYẾN')).toBe(true);
		expect(isCorrect(q, 'tiep tuyen')).toBe(false);
	});
});

describe('answerText', () => {
	it('chữ đáp án theo loại câu', () => {
		expect(answerText(base)).toBe('B');
		expect(answerText({ ...base, type: 'multi', answers: [0, 2] })).toBe('A, C');
		expect(answerText({ ...base, type: 'numeric', value: 2, tolerance: 0.1, unit: 'm' })).toBe('2 ± 0.1 m');
		expect(answerText({ ...base, type: 'short', accept: ['a', 'b'] })).toBe('a / b');
	});
});
