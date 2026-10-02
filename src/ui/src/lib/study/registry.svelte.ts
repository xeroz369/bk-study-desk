// Sổ đăng ký nội dung. content/*.js là script thường (không qua bundle) và gọi:
//   Study.setManifest({...})  Study.addPage({...})  Study.addLesson({...})  Study.addExam({...})
// Thêm bài = thêm file vào content/ và một dòng trong content/manifest.js; không cần build lại giao diện.

import type { Course, Entry, Exam, Lesson, Manifest, Page, Question } from './types';
import { api } from '$lib/api/client';
import { countQuestions, validatePack, type PackQuestion, type PackReport, type StudyPack } from './pack';
import { sanitizeHtml } from './sanitize';
import { quizPacks, type QuizInfo, type SavedQuiz } from './lmsquiz';
import { fingerprint } from './fingerprint';

/** Một gói luyện tập đã cài: nạp được (pack) hoặc lỗi (report.errors). */
export interface InstalledPack {
	file: string;
	pack?: StudyPack;
	report?: PackReport;
	error?: string;
	questions: number;
}

class StudyRegistry {
	manifest = $state<Manifest>({ term: '', courses: [] });
	lessons = $state<Record<string, Lesson>>({});
	pages = $state<Record<string, Page>>({});
	exams = $state<Record<string, Exam>>({});
	loaded = $state(false);
	packs = $state<InstalledPack[]>([]);
	quizInfo = $state<QuizInfo[]>([]);

	setManifest(m: Manifest) {
		this.manifest = m;
	}
	addPage(p: Page) {
		this.pages[p.id] = p;
	}
	addLesson(lesson: Lesson) {
		(lesson.questions ?? []).forEach((q, i) => {
			q.id ||= `${lesson.id}.q${i + 1}`;
			q.lessonId = lesson.id;
			q.fp ||= fingerprint(q);
		});
		this.lessons[lesson.id] = lesson;
	}
	// Đề thi: questionIds tham chiếu câu trong bài học, questions là câu riêng của đề.
	addExam(exam: Exam) {
		(exam.questions ?? []).forEach((q, i) => {
			q.id ||= `${exam.id}.q${i + 1}`;
			q.examId = exam.id;
			q.fp ||= fingerprint(q);
		});
		this.exams[exam.id] = exam;
	}

	/** Môn theo mã (MT1009…); chưa có trong sổ thì tạo môn rỗng (id mon-<mã>, trùng cách addPack đặt id). */
	ensureCourse(code: string, name: string): Course {
		const have = this.manifest.courses.find((c) => c.code?.toUpperCase() === code.toUpperCase());
		if (have) return have;
		this.manifest.courses.push({ id: 'mon-' + code.toLowerCase(), code: code.toUpperCase(), name, units: [] });
		return this.manifest.courses.at(-1)!;
	}

	course(id: string): Course | undefined {
		return this.manifest.courses.find((c) => c.id === id);
	}

	/** Danh sách phẳng các bài theo thứ tự manifest. */
	entries(courseId?: string): Entry[] {
		return this.manifest.courses
			.filter((c) => !courseId || c.id === courseId)
			.flatMap((c) => c.units.flatMap((u) => u.lessons.map((e) => ({ ...e, courseId: c.id, courseName: c.name, unitTitle: u.title }))));
	}
	entry(id: string) {
		return this.entries().find((e) => e.id === id);
	}

	question(id: string): Question | undefined {
		for (const l of Object.values(this.lessons)) {
			const q = l.questions?.find((x) => x.id === id);
			if (q) return q;
		}
		for (const x of Object.values(this.exams)) {
			const q = x.questions?.find((y) => y.id === id);
			if (q) return q;
		}
	}
	lessonQuestions(): Question[] {
		return Object.values(this.lessons).flatMap((l) => l.questions ?? []);
	}
	examQuestions(exam: Exam): Question[] {
		return [...(exam.questionIds ?? []).map((id) => this.question(id)).filter((q): q is Question => !!q), ...(exam.questions ?? [])];
	}

	/**
	 * Random mock exam shaped like the real one (course.blueprint: questions per chapter, minutes, scoring).
	 * Draws from lesson questions, one per fingerprint. `rank`: lower = picked first (e.g. not seen recently);
	 * ties are random. A chapter short on questions gives its share to the others.
	 */
	randomExam(courseId: string, rank: (q: Question) => number = () => 0): Exam | undefined {
		const c = this.course(courseId);
		if (!c) return;
		const bp = c.blueprint ?? { minutes: 50 };
		const base = bp.scoring ?? c.scoring ?? { count: 20, right: 0.5, wrong: -0.1 };
		const seen = new Set<string>();
		const pools = c.units.map((u) =>
			u.lessons
				.flatMap((e) => this.lessons[e.id]?.questions ?? [])
				.filter((q) => !!q.fp && !seen.has(q.fp) && !!seen.add(q.fp))
				.map((q) => ({ q, k: rank(q) + Math.random() }))
				.sort((a, b) => a.k - b.k)
				.map((x) => x.q),
		);
		const sizes = pools.map((p) => p.length);
		const total = bp.units ? bp.units.reduce((a, b) => a + b, 0) : (bp.count ?? base.count);
		let want = bp.units ? pools.map((p, i) => Math.min(bp.units![i] ?? 0, p.length)) : spread(total, sizes);
		// Fill what a thin chapter could not give from chapters that still have questions.
		const short = total - want.reduce((a, b) => a + b, 0);
		if (short > 0) {
			// Top up only from chapters the real exam covers (blueprint > 0), not packs or saved LMS quizzes.
			const extra = spread(
				short,
				sizes.map((n, i) => (!bp.units || (bp.units[i] ?? 0) > 0 ? n - want[i] : 0)),
			);
			want = want.map((n, i) => n + extra[i]);
		}
		const picked = pools.flatMap((p, i) => p.slice(0, want[i]));
		if (!picked.length) return;
		// Fewer questions than the real exam: same 10-point scale, same right/wrong ratio, time scaled down.
		const right = 10 / picked.length;
		const exam: Exam = {
			id: `ngau-nhien-${c.id}-${Date.now()}`,
			courseId: c.id,
			title: `Đề ngẫu nhiên · ${c.name}`,
			minutes: Math.max(5, Math.round((bp.minutes * picked.length) / Math.max(total, 1))),
			scoring: { count: picked.length, right, wrong: (base.wrong / base.right) * right },
			shuffle: { questions: false, options: true },
			questionIds: picked.map((q) => q.id),
		};
		this.exams[exam.id] = exam;
		return exam;
	}

	async load() {
		await loadScript('content/manifest.js');
		const m = this.manifest;
		const files = [
			...(m.pages ?? []).map((p) => p.file),
			...this.entries().map((e) => e.file),
			...(m.exams ?? []).map((x) => x.file),
		].filter((f): f is string => !!f);
		await Promise.all(files.map(loadScript));
		await this.loadPacks();
		await this.loadQuizzes();
		this.loaded = true;
	}

	/** Saved LMS quizzes become a private chapter "Quiz LMS đã lưu" in each course (not written to content/packs). */
	async loadQuizzes() {
		let saved: SavedQuiz[] = [];
		try {
			saved = await api.quizzes();
		} catch {
			return;
		}
		const { packs, info } = quizPacks(saved);
		for (const p of packs) this.addPack(p);
		this.quizInfo = info;
	}

	/** Nạp gói luyện tập trong content/packs: kiểm tra (validatePack), lọc HTML (sanitizeHtml) rồi mới đăng ký. */
	async loadPacks() {
		let list: { file: string; size: number }[] = [];
		try {
			list = await api.packs();
		} catch {
			return;
		}
		const out: InstalledPack[] = [];
		for (const { file, size } of list) {
			try {
				const r = await fetch('content/packs/' + encodeURIComponent(file), { cache: 'no-store' });
				const pack = JSON.parse(await r.text()) as StudyPack;
				const report = validatePack(pack, size);
				if (report.ok) this.addPack(pack);
				out.push({ file, pack, report, questions: report.ok ? countQuestions(pack) : 0 });
			} catch (e) {
				out.push({ file, error: e instanceof Error ? e.message : String(e), questions: 0 });
			}
		}
		this.packs = out;
	}

	/**
	 * Ghép một gói vào sổ: môn cùng mã (course.code) thì thêm chương vào môn đó, chưa có thì tạo môn mới.
	 * Id bài/câu/đề được gắn tiền tố id gói ("<gói>.<bài>") để không đụng nội dung khác và kết quả luyện tập cũ.
	 */
	addPack(p: StudyPack) {
		const S = sanitizeHtml;
		const authors = p.authors.map((a) => a.name).join(', ');
		const code = p.course.code.trim().toUpperCase();
		let course = this.manifest.courses.find((c) => c.code?.toUpperCase() === code);
		if (!course) {
			course = { id: 'mon-' + code.toLowerCase(), code, name: p.course.name, units: [] };
			this.manifest.courses.push(course);
			course = this.manifest.courses.at(-1)!;
		}
		const mapQ = (q: PackQuestion, owner: string, i: number) => packQuestion(q, `${owner}.${q.id ?? 'q' + (i + 1)}`, p.id);
		// Định vị bằng tên: chương trùng tên (đã chuẩn hóa) thì vào chương đó; bài trùng tên trong chương thì
		// thêm kiến thức + câu vào bài đó, không tạo bài trùng. Khác tên thì thêm chương/bài mới.
		const norm = (s: string) => s.normalize('NFC').toLowerCase().replace(/\s+/g, ' ').trim();
		const shuffle = p.settings ? { questions: p.settings.shuffleQuestions, options: p.settings.shuffleOptions } : undefined;
		for (const u of p.units) {
			let unit = course.units.find((x) => norm(x.title) === norm(u.title));
			if (!unit) {
				course.units.push({ title: u.title, lessons: [], pack: { id: p.id, title: p.title, authors } });
				unit = course.units.at(-1)!;
			}
			for (const l of u.lessons) {
				const key = `${p.id}.${l.id}`;
				const sources = (l.sources ?? []).map((s) => ({ file: s.title, pages: s.pages }));
				const sections = (l.sections ?? []).map((s) => ({ title: s.title ? S(s.title) : undefined, html: S(s.body) }));
				const questions = (l.questions ?? []).map((q, i) => mapQ(q, key, i));
				const host = unit.lessons.find((e) => norm(e.title) === norm(l.title));
				if (host) {
					const target = (this.lessons[host.id] ??= { id: host.id, title: host.title, sources: host.sources, sections: [], questions: [] });
					target.sections = [...(target.sections ?? []), ...sections];
					// Same question already in this lesson (another pack, older copy): keep one.
					const have = new Set((target.questions ?? []).map((q) => q.fp));
					const fresh = questions.filter((q) => {
						q.fp = fingerprint(q);
						q.lessonId = host.id;
						return !have.has(q.fp) && !!have.add(q.fp);
					});
					target.questions = [...(target.questions ?? []), ...fresh];
				} else {
					this.addLesson({ id: key, title: l.title, sources, sections, questions, shuffle });
					unit.lessons.push({ id: key, title: l.title, sources });
				}
			}
		}
		for (const x of p.exams ?? []) {
			const id = `${p.id}.${x.id}`;
			this.addExam({
				id,
				courseId: course.id,
				title: x.title,
				minutes: x.minutes,
				scoring: x.scoring,
				shuffle,
				questionIds: (x.questionRefs ?? []).map((r) => `${p.id}.${r}`),
				questions: (x.questions ?? []).map((q, i) => mapQ(q, id, i)),
			});
		}
	}
}

/** Spread `total` over pools by size (largest remainder), never more than a pool holds. */
function spread(total: number, sizes: number[]): number[] {
	const sum = sizes.reduce((a, b) => a + b, 0);
	if (!sum) return sizes.map(() => 0);
	const raw = sizes.map((n) => (Math.min(total, sum) * n) / sum);
	const out = raw.map(Math.floor);
	let rest = Math.min(total, sum) - out.reduce((a, b) => a + b, 0);
	const order = raw.map((r, i) => [r - Math.floor(r), i]).sort((a, b) => b[0] - a[0]);
	for (const [, i] of order) if (rest > 0 && out[i] < sizes[i]) (out[i]++, rest--);
	return out;
}

/** Câu trong gói → câu của sổ (đã lọc HTML). Đúng/Sai thành trắc nghiệm 2 phương án để dùng chung giao diện.
 *  Dùng chung cho nạp gói và cho fingerprint lúc cập nhật gói, để hai bên luôn khớp. */
export function packQuestion(q: PackQuestion, id: string, packId: string): Question {
	const S = sanitizeHtml;
	const base = { id, tag: q.tag, prompt: S(q.prompt), solution: S(q.solution), packId, keepOrder: q.keepOrder };
	switch (q.type ?? 'single') {
		case 'truefalse':
			return { ...base, options: ['Đúng', 'Sai'], answer: q.answer === true ? 0 : 1 };
		case 'multi':
			return { ...base, type: 'multi', options: (q.options ?? []).map(S), answer: -1, answers: q.answers ?? [] };
		case 'numeric':
			return { ...base, type: 'numeric', options: [], answer: -1, value: q.answer as number, tolerance: q.tolerance, unit: q.unit };
		case 'short':
			return { ...base, type: 'short', options: [], answer: -1, accept: q.accept ?? [] };
		default:
			return { ...base, options: (q.options ?? []).map(S), answer: q.answer as number };
	}
}

function loadScript(src: string) {
	return new Promise<boolean>((resolve) => {
		const s = document.createElement('script');
		s.src = src;
		s.onload = () => resolve(true);
		s.onerror = () => {
			console.warn('Không tải được', src);
			resolve(false);
		};
		document.head.appendChild(s);
	});
}

export const Study = new StudyRegistry();
(window as unknown as { Study: StudyRegistry }).Study = Study;
