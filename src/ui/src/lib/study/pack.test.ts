import { describe, expect, it } from 'vitest';
import { countQuestions, validatePack, type StudyPack } from './pack';
import { parseMarkdown } from './markdown';
import { example as file, required } from './test-examples';

// Gói ví dụ: bản private dùng gói đề thật, bản public dựng từ vi-du.md.
const chiaDoiJson = file('hcmut-mt1009-chia-doi.studypack.json');
const example = (): StudyPack => (chiaDoiJson ? JSON.parse(chiaDoiJson) : parseMarkdown(required('vi-du.md')).pack) as StudyPack;
const paths = (p: unknown) => validatePack(p).errors.map((e) => e.path);

describe('validatePack', () => {
	it('gói ví dụ hợp lệ, đếm đúng', () => {
		const r = validatePack(example());
		expect(r.errors).toEqual([]);
		expect(r.ok).toBe(true);
		expect(r.stats.questions).toBeGreaterThan(0);
		expect(countQuestions(example())).toBe(r.stats.questions);
	});

	it('không phải object thì lỗi ngay', () => {
		expect(validatePack(null).ok).toBe(false);
		expect(validatePack([]).errors[0].message).toMatch(/JSON object/);
	});

	it('bắt lỗi khung gói: format, id, version, tác giả, chương', () => {
		const p = { ...example(), format: 'x', id: 'Có Dấu', version: '1', authors: [], units: [] };
		expect(paths(p)).toEqual(expect.arrayContaining(['format', 'id', 'version', 'authors', 'units']));
	});

	it('bắt lỗi câu: đáp án ngoài phương án, multi rỗng, numeric không phải số, short rỗng, id trùng', () => {
		const p = example();
		delete p.exams;
		p.units = [
			{
				title: 'C',
				lessons: [
					{
						id: 'b1',
						title: 'B',
						questions: [
							{ id: 'q1', prompt: 'a', options: ['x', 'y'], answer: 5, solution: 's' },
							{ id: 'q1', prompt: 'b', type: 'multi', options: ['x', 'y'], answers: [], solution: 's' },
							{ prompt: 'c', type: 'numeric', answer: true, solution: 's' },
							{ prompt: 'd', type: 'short', accept: [], solution: 's' },
						],
					},
				],
			},
		];
		expect(paths(p)).toEqual([
			'units[0].lessons[0].questions[0].answer',
			'units[0].lessons[0].questions[1].id',
			'units[0].lessons[0].questions[1].answers',
			'units[0].lessons[0].questions[2].answer',
			'units[0].lessons[0].questions[3].accept',
		]);
	});

	it('chặn script, link ảnh ngoài, thuộc tính sự kiện', () => {
		const p = example();
		const q = p.units[0].lessons[0].questions![0];
		q.prompt = '<script>alert(1)</script>';
		q.solution = '<img src="https://x/y.png">';
		q.options = ['<b onclick="x()">a</b>', 'b'];
		expect(paths(p)).toEqual(
			expect.arrayContaining([
				'units[0].lessons[0].questions[0].prompt',
				'units[0].lessons[0].questions[0].solution',
				'units[0].lessons[0].questions[0].options[0]',
			]),
		);
	});

	it('group không bắt buộc; có thì phải là chuỗi không rỗng', () => {
		const p = example();
		const q = p.units[0].lessons[0].questions![0];
		q.group = 'f1';
		expect(validatePack(p).ok).toBe(true);
		q.group = ' ';
		expect(paths(p)).toEqual(['units[0].lessons[0].questions[0].group']);
	});

	it('đề tham chiếu câu không có trong gói thì lỗi', () => {
		const p = example();
		p.exams = [{ id: 'de-1', title: 'Đề', minutes: 30, questionRefs: ['khong-co.q1'] }];
		expect(paths(p)).toEqual(['exams[0].questionRefs[0]']);
	});
});
