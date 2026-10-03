// @vitest-environment jsdom
import { beforeEach, describe, expect, it } from 'vitest';
import { courseActions, LABELS, lessonActions, unitActions } from './actions';
import { canRandomExam, Study } from './registry.svelte';
import type { Course, Question } from './types';

const qs = (lesson: string, n: number): Question[] =>
	Array.from({ length: n }, (_, i) => ({ id: '', prompt: `${lesson} câu ${i + 1}`, options: ['a', 'b'], answer: 0, solution: 'x' }));

function setup(n: number, blueprint = false): Course {
	const c: Course = {
		id: 'ppt',
		code: 'MT1009',
		name: 'Phương pháp tính',
		units: [
			{ title: 'Chương 1', lessons: [{ id: 'ppt-1', title: 'Bài 1' }] },
			{ title: 'Quiz LMS đã lưu', lessons: [], pack: { id: 'quiz-lms-mt1009', title: 'q', authors: '' } },
		],
		...(blueprint ? { blueprint: { minutes: 50 } } : {}),
	};
	Study.setManifest({ term: '261', courses: [c] });
	Study.lessons = {};
	Study.addLesson({ id: 'ppt-1', title: 'Bài 1', questions: qs('ppt-1', n) });
	return Study.course('ppt')!;
}

describe('actions', () => {
	beforeEach(() => setup(12));

	it('menu môn dùng nhãn chung, có đề ngẫu nhiên khi đủ câu', () => {
		const labels = courseActions(Study.course('ppt')!, () => {}).map((m) => m.label);
		expect(labels).toEqual([LABELS.course.open, LABELS.course.create, LABELS.course.import, LABELS.course.export, LABELS.randomExam]);
		expect(labels).toContain('Đề ngẫu nhiên');
	});

	it('một luật đề ngẫu nhiên cho mọi chỗ', () => {
		expect(canRandomExam(setup(9))).toBe(false);
		expect(courseActions(setup(9), () => {}).map((m) => m.label)).not.toContain(LABELS.randomExam);
		expect(canRandomExam(setup(3, true))).toBe(true);
		expect(canRandomExam(setup(0, true))).toBe(false);
	});

	it('chương quiz LMS không có menu Tạo, Nhập, Xuất', () => {
		const c = Study.course('ppt')!;
		expect(unitActions(c, 0).map((m) => m.label)).toEqual([LABELS.unit.create, LABELS.unit.import, LABELS.unit.export]);
		expect(unitActions(c, 1)).toEqual([]);
	});

	it('menu bài: Làm bài rồi Tạo, Nhập, Xuất', () => {
		const m = lessonActions('ppt-1');
		expect(m[0]).toMatchObject({ label: LABELS.lesson.open, primary: true });
		expect(m.slice(1).map((x) => x.label)).toEqual([LABELS.lesson.create, LABELS.lesson.import, LABELS.lesson.export]);
	});
});
