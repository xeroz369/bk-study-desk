// Một chỗ dựng menu chuột phải và nhãn lệnh cho môn, chương, bài, gói, quiz LMS.
// Trang Luyện tập, trang bài và Kho quiz dùng chung nên cùng một việc luôn cùng một chữ.

import type { MenuItem } from '$lib/menu.svelte';
import { canRandomExam, Study } from './registry.svelte';
import { Progress, today } from './progress.svelte';
import { RANDOM_EXAM } from './exam';
import { canMix, interleave, MIX_COUNT } from './interleave';
import type { StudyPack } from './pack';
import type { QuizInfo } from './lmsquiz';
import { exportMarkdown, lessonCan, scopeOfLesson, scopePath, shareablePack } from './transfer';
import type { Course, Question } from './types';

export const LABELS = {
	course: { open: 'Mở môn', create: 'Tạo câu', import: 'Nhập vào môn này', export: 'Xuất để chia sẻ' },
	unit: { create: 'Tạo câu trong chương', import: 'Nhập vào chương này', export: 'Xuất để chia sẻ' },
	lesson: { open: 'Làm bài', create: 'Tạo câu trong bài', import: 'Nhập vào bài này', export: 'Xuất để chia sẻ' },
	pack: { remove: 'Gỡ quiz này', exportLater: 'sau khi quiz đóng', broken: 'gói đang lỗi' },
	quiz: { review: 'Ôn lại', similar: 'Câu tương tự (nhờ AI)', recall: 'Ghi lại câu còn nhớ' },
	randomExam: RANDOM_EXAM,
	mix: 'Luyện trộn',
	/** nút ngắn trên đầu trang, đầu khối */
	button: { create: 'Tạo', import: 'Nhập', export: 'Xuất' },
} as const;

const go = (hash: string) => () => (location.hash = hash);

/** Tạo, Nhập, Xuất cho một chỗ (đường dẫn như scopePath: môn[/chương[/bài]]). */
function soanItems(path: string, l: { create: string; import: string; export: string }, can = { tao: true, nhap: true, xuat: true }) {
	const items: MenuItem[] = [];
	if (can.tao) items.push({ label: l.create, run: go(`#soan/tao/${path}`) });
	if (can.nhap) items.push({ label: l.import, run: go(`#soan/nhap/${path}`) });
	if (can.xuat) items.push({ label: l.export, run: go(`#soan/xuat/${path}`) });
	return items.map((m, i) => ({ ...m, sep: i === 0 }));
}

// Chưa làm hoặc tới hạn ôn hôm nay lên trước; ngẫu nhiên trong từng nhóm do randomExam lo.
const fresh = (q: Question) => {
	const r = q.fp ? Progress.state.srs[q.fp] : undefined;
	return !r || r.due <= today() ? 0 : 1;
};

/** Ra đề ngẫu nhiên và mở trang thi; false khi môn chưa có câu nào để ra đề. */
export function startRandomExam(courseId: string): boolean {
	const x = Study.randomExam(courseId, fresh);
	if (x) location.hash = '#thi/' + x.id;
	return !!x;
}

/** Môn trộn được không (ít nhất hai chương có câu). */
export const courseCanMix = (c: Course) => canMix(Study.unitQuestions(c));

/** Một lượt Luyện trộn của môn: câu xen kẽ các chương, chưa làm và tới hạn ôn lên trước (cùng thứ tự ưu tiên với Đề ngẫu nhiên). */
export function mixQuestions(courseId: string): Question[] {
	const c = Study.course(courseId);
	return c ? interleave(Study.unitQuestions(c), MIX_COUNT, fresh) : [];
}

/** Menu của một môn. onRandomExam: trang tự báo khi không ra được đề. */
export function courseActions(c: Course, onRandomExam: (courseId: string) => void): MenuItem[] {
	return [
		{ label: LABELS.course.open, primary: true, run: go('#luyen-tap/' + c.id) },
		...soanItems(c.id, LABELS.course),
		...(canRandomExam(c) ? [{ label: RANDOM_EXAM, sep: true, run: () => onRandomExam(c.id) }] : []),
		...(courseCanMix(c) ? [{ label: LABELS.mix, run: go('#luyen-tron/' + c.id) }] : []),
	];
}

/** Chương quiz LMS đã lưu: không Tạo, Nhập, Xuất cả chương (từng bài tự quyết). */
export const isLmsUnit = (u: Course['units'][number]) => !!u.pack?.id.startsWith('quiz-lms');

/** Menu đầu khối chương. */
export function unitActions(c: Course, ui: number): MenuItem[] {
	const u = c.units[ui];
	return !u || isLmsUnit(u) ? [] : soanItems(`${c.id}/${ui}`, LABELS.unit);
}

/** Menu của một bài: Làm bài, rồi Tạo, Nhập, Xuất theo quyền của bài (quiz LMS đã lưu bị khóa bớt). */
export function lessonActions(lessonId: string): MenuItem[] {
	const sc = scopeOfLesson(lessonId);
	return [
		{ label: LABELS.lesson.open, primary: true, run: go('#bai/' + lessonId) },
		...(sc ? soanItems(scopePath(sc), LABELS.lesson, lessonCan(lessonId)) : []),
	];
}

const courseOfPack = (p: StudyPack) => Study.manifest.courses.find((c) => c.code?.toUpperCase() === p.course.code.trim().toUpperCase());

/** Menu của một gói đã cài. ok: gói nạp được (không lỗi). */
export function packActions(p: StudyPack, ok: boolean, remove: () => void): MenuItem[] {
	const c = ok ? courseOfPack(p) : undefined;
	// Questions recalled from an LMS quiz still open stay on this computer (shareablePack leaves them out).
	const share = ok ? shareablePack(p) : null;
	return [
		...(c
			? [
					{ label: LABELS.course.open, primary: true, run: go('#luyen-tap/' + c.id) },
					...soanItems(c.id, LABELS.course, { tao: true, nhap: true, xuat: false }),
				]
			: []),
		{
			label: LABELS.course.export,
			sep: !!c,
			hint: share ? undefined : ok ? LABELS.pack.exportLater : LABELS.pack.broken,
			disabled: !share,
			run: () => share && exportMarkdown(share),
		},
		{ label: LABELS.pack.remove, sep: true, run: remove },
	];
}

/** Menu của một quiz LMS đã lưu. recall: ghi lại câu còn nhớ khi quiz không cho xem lại. */
export function quizActions(i: QuizInfo, recall: () => void): MenuItem[] {
	const sc = i.lessonId ? scopeOfLesson(i.lessonId) : null;
	if (!i.lessonId || !sc) return [{ label: LABELS.quiz.recall, primary: true, run: recall }];
	return [
		{ label: LABELS.quiz.review, primary: true, run: go('#bai/' + i.lessonId) },
		{ label: LABELS.quiz.similar, run: go(`#soan/ai/${scopePath(sc)}`) },
		...(i.shareable ? [{ label: LABELS.lesson.export, run: go(`#soan/xuat/${scopePath(sc)}`) }] : []),
	];
}
