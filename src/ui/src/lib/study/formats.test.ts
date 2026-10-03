// @vitest-environment jsdom
// GIFT, Aiken, Moodle XML: đọc file mẫu và xuất rồi đọc lại phải giữ loại câu và đáp án. Moodle XML cần DOMParser.
import { describe, expect, it } from 'vitest';
import { detectFormat, parseAiken, parseGift, parseMoodleXml, toGift, toMoodleXml } from './formats';
import { parseMarkdown } from './markdown';
import { validatePack, type PackQuestion, type StudyPack } from './pack';
import { example, required } from './test-examples';

const viDu = required('vi-du.md');
const chiaDoiJson = example('hcmut-mt1009-chia-doi.studypack.json');

const key = (q: PackQuestion) => JSON.stringify([q.type ?? 'single', q.answer, q.answers, q.accept, q.tolerance ?? 0, q.options?.length]);
const lessonQs = (p: StudyPack) => p.units.flatMap((u) => u.lessons.flatMap((l) => l.questions ?? []));
const sources: [string, StudyPack][] = [
	['vi-du.md', parseMarkdown(viDu).pack],
	...(chiaDoiJson ? [['hcmut-mt1009-chia-doi.studypack.json', JSON.parse(chiaDoiJson) as StudyPack] as [string, StudyPack]] : []),
];

describe('detectFormat', () => {
	it('nhận đúng từng định dạng', () => {
		expect(detectFormat(viDu)).toBe('markdown');
		if (chiaDoiJson) expect(detectFormat(chiaDoiJson)).toBe('studypack');
		expect(detectFormat(JSON.stringify(sources[0][1]))).toBe('studypack');
		expect(detectFormat(toGift(sources[0][1]))).toBe('gift');
		expect(detectFormat(toMoodleXml(sources[0][1]))).toBe('moodlexml');
		expect(detectFormat('Câu hỏi?\nA. 1\nB. 2\nANSWER: B')).toBe('aiken');
		expect(detectFormat('chữ thường')).toBeNull();
	});
});

describe('GIFT', () => {
	it.each(sources)('%s: xuất GIFT rồi đọc lại giữ loại câu, đáp án', (_n, p) => {
		const back = parseGift(toGift(p)).lessons.flatMap((l) => l.questions ?? []);
		expect(back.map(key)).toEqual(lessonQs(p).map(key));
	});

	it('đọc câu viết tay: category, tiêu đề, đúng/sai, điền số, điền chữ', () => {
		const r = parseGift(
			[
				'$CATEGORY: $course$/top/PPT/Chia đôi',
				'',
				'::c1:: 2 + 2 = ? {=4 ~3 ~5}',
				'',
				'Trái đất tròn. {T}',
				'',
				'Căn bậc hai của 2 {#1.414:0.001}',
				'',
				'Tên phương pháp lặp có đạo hàm? {=Newton =Newton-Raphson}',
			].join('\n'),
		);
		expect(r.lessons.map((l) => l.title)).toEqual(['Chia đôi']);
		const qs = r.lessons[0].questions!;
		expect(qs[0]).toMatchObject({ tag: 'c1', answer: 0, options: ['4', '3', '5'] });
		expect(qs[1]).toMatchObject({ type: 'truefalse', answer: true });
		expect(qs[2]).toMatchObject({ type: 'numeric', answer: 1.414, tolerance: 0.001 });
		expect(qs[3]).toMatchObject({ type: 'short', accept: ['Newton', 'Newton-Raphson'] });
	});
});

describe('Moodle XML', () => {
	it.each(sources)('%s: xuất Moodle XML rồi đọc lại giữ loại câu, đáp án, bài', (_n, p) => {
		const r = parseMoodleXml(toMoodleXml(p));
		expect(r.lessons.map((l) => l.title)).toEqual(p.units.flatMap((u) => u.lessons.map((l) => l.title)));
		expect(r.lessons.flatMap((l) => l.questions ?? []).map(key)).toEqual(lessonQs(p).map(key));
	});

	it('ảnh nhúng đi qua @@PLUGINFILE@@ và quay lại data URI', () => {
		const img = 'data:image/png;base64,iVBORw0KGgo=';
		const p: StudyPack = {
			...sources[0][1],
			units: [
				{
					title: 'C',
					lessons: [
						{
							id: 'b1',
							title: 'B',
							questions: [{ prompt: `<p>Hình <img src="${img}"></p>`, options: ['a', 'b'], answer: 1, solution: 'x' }],
						},
					],
				},
			],
		};
		const xml = toMoodleXml(p);
		expect(xml).toContain('@@PLUGINFILE@@/img1.png');
		expect(parseMoodleXml(xml).lessons[0].questions![0].prompt).toContain(img);
	});

	it('loại chưa hỗ trợ được bỏ qua kèm ghi chú', () => {
		const r = parseMoodleXml(
			'<quiz><question type="essay"><name><text>e</text></name><questiontext><text>x</text></questiontext></question></quiz>',
		);
		expect(r.lessons).toEqual([]);
		expect(r.notes.join()).toMatch(/tự luận/);
	});

	it('XML hỏng thì báo lỗi, không ném', () => {
		expect(parseMoodleXml('<quiz><question').notes[0]).toMatch(/XML lỗi/);
	});
});

describe('Aiken', () => {
	it('đọc câu một đáp án, bỏ câu thiếu phương án', () => {
		const r = parseAiken(['Thủ đô của Việt Nam?', 'A. Hà Nội', 'B. Huế', 'ANSWER: A', '', 'Câu lỗi', 'ANSWER: B'].join('\n'));
		const qs = r.lessons[0].questions!;
		expect(qs).toHaveLength(1);
		expect(qs[0]).toMatchObject({ options: ['Hà Nội', 'Huế'], answer: 0 });
		expect(r.notes.some((n) => /Bỏ một câu/.test(n))).toBe(true);
	});

	it('gói dựng từ câu Aiken qua được validatePack', () => {
		const qs = parseAiken('Đề?\nA. x\nB. y\nANSWER: B').lessons[0].questions!;
		const p: StudyPack = { ...sources[0][1], units: [{ title: 'C', lessons: [{ id: 'aiken', title: 'B', questions: qs }] }] };
		expect(validatePack(p).errors).toEqual([]);
	});
});
