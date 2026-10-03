// Study Markdown: đọc, ghi, đọc lại phải giữ mọi câu (cùng kiểu với studypack/test-roundtrip.ts, chạy trên file ví dụ).
import { describe, expect, it } from 'vitest';
import { parseMarkdown, writeMarkdown } from './markdown';
import { plainText } from './text';
import type { PackQuestion, StudyPack } from './pack';
import { example, required } from './test-examples';

const viDu = required('vi-du.md');

const key = (q: PackQuestion) =>
	JSON.stringify([q.type ?? 'single', q.answer, q.answers, q.accept, q.tolerance, q.options?.length, q.keepOrder, q.group, q.tag]);
const all = (p: StudyPack) => [
	...p.units.flatMap((u) => u.lessons.flatMap((l) => l.questions ?? [])),
	...(p.exams ?? []).flatMap((x) => x.questions ?? []),
];

describe('parseMarkdown + writeMarkdown', () => {
	it.each(
		['vi-du.md', 'hcmut-ee1009-giua-ky.md', 'hcmut-mt1009-giua-ky.md', 'hcmut-mt1009-chia-doi.md']
			.map((n) => [n, example(n)] as const)
			.filter((x): x is readonly [string, string] => x[1] !== undefined),
	)('%s: đọc không lỗi, ghi rồi đọc lại giữ nguyên mọi câu', (_name, md) => {
		const a = parseMarkdown(md);
		expect(a.errors).toEqual([]);
		expect(all(a.pack).length).toBeGreaterThan(0);
		const { md: out, images } = writeMarkdown(a.pack);
		const b = parseMarkdown(out, images);
		expect(b.errors).toEqual([]);
		expect(all(b.pack).map(key)).toEqual(all(a.pack).map(key));
		// HTML có thể khác chút (<br> thành đoạn mới) nhưng chữ phải giữ nguyên.
		const text = (q: PackQuestion) => plainText(q.prompt).replace(/\s+/g, '');
		expect(all(b.pack).map(text)).toEqual(all(a.pack).map(text));
		expect(b.pack.course).toEqual(a.pack.course);
		expect(b.pack.units.map((u) => u.title)).toEqual(a.pack.units.map((u) => u.title));
	});

	it('vi-du.md: đủ bốn loại câu, đáp án đúng', () => {
		const qs = all(parseMarkdown(viDu).pack);
		expect(qs.map((q) => q.type ?? 'single')).toEqual(['single', 'multi', 'truefalse', 'numeric']);
		expect(qs[0].answer).toBe(1);
		expect(qs[1].answers).toEqual([0, 1]);
		expect(qs[2].answer).toBe(true);
		expect(qs[3]).toMatchObject({ answer: 1, tolerance: 0.001 });
	});

	it('nhóm và giữ thứ tự trong dòng ###, ghi ra rồi đọc lại vẫn còn', () => {
		const md = [
			'---',
			'mon: MT1009 Phương pháp tính',
			'tac-gia: A',
			'---',
			'# Chương 1',
			'## Bài 1',
			'### Câu 1 · GK251 · nhóm: f1',
			'Cho f(x) = x. Tính f(1).',
			'- [x] 1',
			'- [ ] 2',
			'> f(1) = 1.',
			'### Câu 2 · nhóm: f1 · giữ thứ tự',
			'Tính f(2).',
			'- [ ] 1',
			'- [x] 2',
			'- [ ] Cả hai',
			'> f(2) = 2.',
		].join('\n');
		const a = parseMarkdown(md);
		expect(a.errors).toEqual([]);
		const [q1, q2] = all(a.pack);
		expect(q1).toMatchObject({ group: 'f1', tag: 'GK251' });
		expect(q1.keepOrder).toBeUndefined();
		expect(q2).toMatchObject({ group: 'f1', keepOrder: true });
		expect(q2.tag).toBeUndefined();
		const b = parseMarkdown(writeMarkdown(a.pack).md);
		expect(all(b.pack).map(key)).toEqual(all(a.pack).map(key));
	});

	it('báo lỗi đúng dòng', () => {
		const r = parseMarkdown(['# C', '## B', '### Câu 1', 'Đề', '- [x] a', '- [ ] b'].join('\n'));
		expect(r.errors.some((e) => e.line === 3 && /lời giải/.test(e.message))).toBe(true);
	});
});
