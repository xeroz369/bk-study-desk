// Saved LMS quiz reviews (data/lms-quiz, see LmsSource.CollectQuizzesAsync) -> practice packs, one per course.
// Only what the review page shows is used. A question whose right answer is hidden is kept out of practice:
// we never guess answers.

import { parseNumber } from './grade';
import { normText } from './text';
import { PACK_FORMAT, type PackQuestion, type StudyPack } from './pack';
import { APP_NAME } from '$lib/app-name';

export interface SavedQuiz {
	file: string;
	quiz: string;
	subject: string;
	code?: string;
	course?: number;
	attempt: number;
	finished?: number;
	grade?: number | string | null;
	closesAt?: number | null;
	savedAt?: number;
	answers?: boolean;
	noReview?: boolean;
	questions?: { type: string; html: string; mark?: string; maxmark?: number; status?: string }[];
}

export interface QuizInfo {
	quiz: SavedQuiz;
	lessonId: string; // full lesson id in the registry (<pack>.<lesson>), empty when nothing is usable
	usable: number; // questions with a known right answer
	unknown: number; // right answer hidden by LMS
	/** Shared only once the quiz has closed for everyone. */
	shareable: boolean;
}

/** Saved LMS quiz lessons (pack quiz-lms-<code>) and questions recalled from them (quiz-lms-ghi-<code>). */
export const isLmsQuizLesson = (lessonId: string) => lessonId.startsWith('quiz-lms-');
export const isLmsQuiz = (q: { packId?: string; lessonId?: string }) =>
	!!q.packId?.startsWith('quiz-lms-') || isLmsQuizLesson(q.lessonId ?? '');
/** Questions written down from memory for a quiz with no review (pack quiz-lms-ghi-<code>). */
export const RECALL_PREFIX = 'quiz-lms-ghi-';
export const isRecallLesson = (lessonId: string) => lessonId.startsWith(RECALL_PREFIX);

export const UNIT_TITLE = 'Quiz LMS đã lưu';
const slug = (s: string) =>
	s
		.normalize('NFD')
		.replace(/[̀-ͯ]/g, '')
		.replace(/[đĐ]/g, 'd')
		.toLowerCase()
		.replace(/[^a-z0-9]+/g, '-')
		.replace(/^-+|-+$/g, '')
		.slice(0, 40) || 'quiz';
const dateText = (t?: number) => (t ? new Date(t * 1000).toLocaleDateString('vi-VN') : '');

/** One question of a Moodle review page -> pack question (or null when the answer is not shown). */
export function parseReview(html: string): PackQuestion | null {
	const doc = new DOMParser().parseFromString(html, 'text/html');
	const prompt = doc.querySelector('.qtext')?.innerHTML.trim() ?? '';
	if (!prompt) return null;
	const rightEl = doc.querySelector('.rightanswer');
	const rightText = (rightEl?.textContent ?? '').replace(/^.*?(is|are|là)\s*:\s*/i, '').trim();
	const feedback = doc.querySelector('.generalfeedback')?.innerHTML.trim() ?? '';
	const state = doc.querySelector('.state')?.textContent?.trim() ?? '';
	const rows = [...doc.querySelectorAll('.answer > div')].filter((r) => r.querySelector('input'));
	let you = '';
	let q: PackQuestion | null = null;
	if (rows.length) {
		const label = (r: Element) =>
			(r.querySelector('[data-region="answer-label"] .flex-fill, .flex-fill') ?? r.querySelector('label') ?? r).innerHTML.trim();
		const text = (r: Element) =>
			normText((r.querySelector('[data-region="answer-label"] .flex-fill, .flex-fill') ?? r).textContent ?? '');
		const options = rows.map(label);
		const multi = !!rows[0].querySelector('input[type="checkbox"]');
		const picked = rows.map((r, i) => (r.querySelector('input')?.hasAttribute('checked') ? i : -1)).filter((i) => i >= 0);
		const right = new Set(rows.map((r, i) => (r.classList.contains('correct') ? i : -1)).filter((i) => i >= 0));
		const want = normText(rightText);
		if (want)
			rows.forEach((r, i) => {
				const t = text(r);
				if (t && (t === want || (multi && want.includes(t)))) right.add(i);
			});
		you = picked.map((i) => 'ABCDEFGH'[i]).join(', ');
		const answers = [...right].sort((a, b) => a - b);
		if (!answers.length || (!multi && answers.length > 1)) return null;
		q = multi ? { type: 'multi', prompt, options, answers, solution: '' } : { prompt, options, answer: answers[0], solution: '' };
	} else {
		you = (doc.querySelector('input[type="text"]') as HTMLInputElement | null)?.getAttribute('value') ?? '';
		if (!rightText) return null;
		const n = parseNumber(rightText.replace(/\s*\(.*\)\s*$/, ''));
		const unit = rightText.match(/\(([^)]+)\)\s*$/)?.[1];
		q =
			n !== null
				? { type: 'numeric', prompt, answer: n, ...(unit ? { unit } : {}), solution: '' }
				: { type: 'short', prompt, accept: [rightText], solution: '' };
	}
	const result = /incorrect|sai/i.test(state)
		? 'sai'
		: /partially|một phần/i.test(state)
			? 'đúng một phần'
			: /correct|đúng/i.test(state)
				? 'đúng'
				: '';
	q.solution =
		[
			rightEl ? `<p><b>Đáp án đúng:</b> ${rightEl.innerHTML.replace(/^[\s\S]*?(is|are|là)\s*:\s*/i, '')}</p>` : '',
			feedback,
			you ? `<p><small>Lần làm trên LMS: bạn chọn ${you}${result ? ` (${result})` : ''}.</small></p>` : '',
		].join('') || '<p>(LMS không có lời giải cho câu này.)</p>';
	return q;
}

/** All saved quizzes -> one pack per course code (unit "Quiz LMS đã lưu", one lesson per attempt). */
export function quizPacks(saved: SavedQuiz[], now = Date.now() / 1000): { packs: StudyPack[]; info: QuizInfo[] } {
	const byCode = new Map<string, StudyPack>();
	const info: QuizInfo[] = [];
	for (const s of saved.sort((a, b) => (a.finished ?? 0) - (b.finished ?? 0))) {
		const code = (s.code ?? '').toUpperCase();
		const parsed = (s.questions ?? []).map((x) => parseReview(x.html));
		const questions = parsed.filter((x): x is PackQuestion => !!x).map((x, i) => ({ ...x, id: `q${i + 1}` }));
		const local = `${slug(s.quiz)}-${s.attempt}`;
		const lessonId = code && questions.length ? `${slug(`quiz-lms-${code}`)}.${local}` : '';
		info.push({
			quiz: s,
			lessonId,
			usable: questions.length,
			unknown: parsed.length - questions.length,
			shareable: !!s.closesAt && s.closesAt < now,
		});
		if (!code || !questions.length) continue;
		if (!byCode.has(code))
			byCode.set(code, {
				format: PACK_FORMAT,
				id: slug(`quiz-lms-${code}`),
				title: `${s.subject}: quiz LMS đã lưu`,
				version: '1.0.0',
				language: 'vi',
				course: { code, name: s.subject, school: 'HCMUT' },
				authors: [{ name: 'Lưu từ LMS' }],
				createdWith: `${APP_NAME} (kho quiz LMS)`,
				verified: { method: 'đáp án theo trang xem lại của LMS' },
				units: [{ title: UNIT_TITLE, lessons: [] }],
			});
		byCode.get(code)!.units[0].lessons.push({
			// Add the date only when the quiz name does not already carry one.
			id: local,
			title: s.finished && !/\d{1,2}\/\d{1,2}/.test(s.quiz) ? `${s.quiz} (${dateText(s.finished)})` : s.quiz,
			sources: [{ title: `Quiz LMS: ${s.quiz}`, ...(s.finished ? { pages: `làm ngày ${dateText(s.finished)}` } : {}) }],
			questions,
		});
	}
	return { packs: [...byCode.values()], info };
}
