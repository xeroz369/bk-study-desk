// Kết quả luyện tập: data/ket-qua.json qua /api/state. Bản sao trong localStorage chỉ để không mất
// câu vừa làm nếu một lần ghi thất bại; lần mở sau gộp hai bản (merge) rồi ghi lại.

import { api } from '$lib/api/client';
import { Study } from './registry.svelte';
import type { Answer, ExamAttempt, LessonStatus, NoteRecord, ProgressState, Question, QuestionRecord, SrsRecord } from './types';

const KEY = 'so-on-tap-hk261';

const empty = (): ProgressState => ({ version: 1, updatedAt: null, questions: {}, lessons: {}, exams: [], srs: {}, notes: {}, times: {} });

/** Leitner boxes: days until the next review. Wrong -> box 1 (tomorrow); right -> next box. */
const INTERVAL = [0, 1, 3, 7, 14, 30];
/** Local calendar day, YYYY-MM-DD. */
export function today(offsetDays = 0) {
	const d = new Date(Date.now() + offsetDays * 86_400_000);
	return new Date(d.getTime() - d.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
}

function normalize(s: Partial<ProgressState> | null | undefined): ProgressState {
	if (!s || typeof s !== 'object') return empty();
	return {
		version: 1,
		updatedAt: s.updatedAt ?? null,
		questions: s.questions ?? {},
		lessons: s.lessons ?? {},
		exams: Array.isArray(s.exams) ? s.exams : [],
		srs: s.srs ?? {},
		notes: s.notes ?? {},
		times: s.times ?? {},
	};
}

// Gộp hai bản: câu hỏi lấy bản mới hơn, nhưng "đúng lần đầu" lấy từ bản cũ hơn.
function merge(a: ProgressState, b: ProgressState): ProgressState {
	const out = normalize(structuredClone(a));
	for (const [id, rb] of Object.entries(b.questions)) {
		const ra = out.questions[id];
		if (!ra) {
			out.questions[id] = rb;
			continue;
		}
		if (ra.at === rb.at) continue;
		const [older, newer] = (ra.at ?? '') < (rb.at ?? '') ? [ra, rb] : [rb, ra];
		out.questions[id] = {
			...newer,
			firstTry: older.firstTry,
			correct: ra.correct || rb.correct,
			tries: Math.max(ra.tries ?? 0, rb.tries ?? 0),
			reviewedOk: ra.reviewedOk || rb.reviewedOk,
		};
	}
	for (const [id, lb] of Object.entries(b.lessons)) {
		const la = out.lessons[id];
		if (!la || (la.openedAt ?? '') < (lb.openedAt ?? '')) out.lessons[id] = lb;
	}
	const key = (x: ExamAttempt) => x.examId + '|' + x.startedAt;
	const seen = new Set(out.exams.map(key));
	b.exams.forEach((x) => !seen.has(key(x)) && out.exams.push(x));
	out.exams.sort((x, y) => x.startedAt.localeCompare(y.startedAt));
	// Review schedule / notes: keep the most recently touched record.
	for (const [fp, r] of Object.entries(b.srs)) if (!out.srs[fp] || out.srs[fp].last < r.last) out.srs[fp] = r;
	for (const [fp, r] of Object.entries(b.notes)) if (!out.notes[fp] || out.notes[fp].at < r.at) out.notes[fp] = r;
	out.times = { ...out.times, ...b.times };
	return out;
}

class ProgressStore {
	state = $state<ProgressState>(empty());
	saveError = $state('');
	private timer: ReturnType<typeof setTimeout> | undefined;

	async init() {
		const local = readLocal();
		try {
			const server = normalize(await api.state());
			const merged = merge(server, local);
			this.state = merged;
			const keys = ['questions', 'lessons', 'exams', 'srs', 'notes', 'times'] as const;
			if (keys.some((k) => JSON.stringify(merged[k]) !== JSON.stringify(server[k]))) this.save();
		} catch {
			this.state = local;
			this.saveError = 'Không đọc được data\\ket-qua.json. Kết quả tạm giữ trong app.';
		}
		writeLocal(this.state);
	}

	save() {
		clearTimeout(this.timer);
		this.timer = setTimeout(() => void this.flush(), 300);
	}

	/** After Study.load (needs question fingerprints): first run starts the review schedule from past answers. */
	seedSchedule() {
		if (Object.keys(this.state.srs).length) return;
		seedSrs(this.state);
		if (Object.keys(this.state.srs).length) this.save();
	}

	/** Write now (before a reload); normally save() batches writes. */
	async flush() {
		clearTimeout(this.timer);
		this.state.updatedAt = new Date().toISOString();
		const snap = $state.snapshot(this.state) as ProgressState;
		writeLocal(snap);
		this.saveError = (await api.saveState(snap).catch(() => null))
			? ''
			: 'Không ghi được vào data\\ket-qua.json. Kết quả vẫn được giữ và sẽ ghi lại ở lần lưu sau.';
	}

	q(id: string): QuestionRecord | undefined {
		return this.state.questions[id];
	}

	recordAnswer(id: string, chosen: Answer, correct: boolean) {
		const prev = this.state.questions[id];
		this.state.questions[id] = {
			tries: (prev?.tries ?? 0) + 1,
			firstTry: prev?.tries ? prev.firstTry : correct,
			correct: correct || !!prev?.correct,
			lastCorrect: correct,
			reviewedOk: !!prev?.reviewedOk,
			chosen,
			at: new Date().toISOString(),
		};
		const fp = Study.question(id)?.fp;
		if (fp) this.srsAnswer(fp, correct);
		this.save();
	}

	// ------------------------------------------------------------------ review schedule (Leitner)

	/** Only the first answer of the day moves a question between boxes (retries in practice do not). */
	srsAnswer(fp: string, correct: boolean) {
		const day = today();
		const r = this.state.srs[fp];
		if (r?.last === day) {
			if (!correct && r.box > 1) Object.assign(r, { box: 1, due: today(1) });
			return;
		}
		const box = correct ? Math.min(5, (r?.box ?? 1) + 1) : 1;
		this.state.srs[fp] = {
			box,
			due: today(INTERVAL[box]),
			last: day,
			seen: (r?.seen ?? 0) + 1,
			wrong: (r?.wrong ?? 0) + (correct ? 0 : 1),
		};
		this.save();
	}

	/** Questions due today (one per fingerprint), hardest first. Optional course filter. */
	dueQuestions(courseId?: string, limit = 40): Question[] {
		const day = today();
		// Lesson questions first (so a question that is in both shows with its lesson), then exam-only questions.
		const exams = Object.values(Study.exams).filter((x) => !courseId || x.courseId === courseId);
		const pool = [
			...(courseId ? Study.entries(courseId).flatMap((e) => Study.lessons[e.id]?.questions ?? []) : Study.lessonQuestions()),
			...exams.flatMap((x) => x.questions ?? []),
		];
		const byFp = new Map<string, Question>();
		for (const q of pool) if (q.fp && !byFp.has(q.fp)) byFp.set(q.fp, q);
		return [...byFp.entries()]
			.map(([fp, q]) => ({ q, r: this.state.srs[fp] }))
			.filter((x): x is { q: Question; r: SrsRecord } => !!x.r && x.r.due <= day)
			.sort((a, b) => a.r.box - b.r.box || a.r.due.localeCompare(b.r.due))
			.slice(0, limit)
			.map((x) => x.q);
	}

	// ------------------------------------------------------------------ timing, notes

	recordTime(fp: string | undefined, seconds: number) {
		if (!fp || seconds <= 0 || seconds > 3600) return;
		this.state.times[fp] = Math.round(seconds);
		this.save();
	}

	/** Notes that still say something (text or flag); cleared ones are kept only as merge markers. */
	activeNotes(): [string, NoteRecord][] {
		return Object.entries(this.state.notes).filter(([, n]) => !!n.text?.trim() || !!n.flag);
	}

	note(fp: string | undefined): NoteRecord | undefined {
		return fp ? this.state.notes[fp] : undefined;
	}

	setNote(fp: string | undefined, patch: Partial<NoteRecord>) {
		if (!fp) return;
		// Cleared notes stay as an empty record with a newer time, so merging with an older copy cannot bring them back.
		this.state.notes[fp] = { ...this.state.notes[fp], ...patch, at: new Date().toISOString() };
		this.save();
	}

	/** A pack was updated and question ids changed: carry results over (old id -> new id; oldIds = all ids the pack had). */
	migrateIds(map: Map<string, string>, oldIds: string[] = []) {
		// Move from a snapshot: ids can swap (old q1 -> q2 while a new question takes q1).
		const snap = { ...this.state.questions };
		const targets = new Set(map.values());
		let moved = 0;
		for (const [from, to] of map) {
			if (from === to || !snap[from]) continue;
			this.state.questions[to] = snap[from];
			if (!targets.has(from)) delete this.state.questions[from];
			moved++;
		}
		// Old ids no question kept (removed, or the id now belongs to a different question): drop their results.
		for (const id of oldIds)
			if (!targets.has(id) && snap[id] && this.state.questions[id] === snap[id]) (delete this.state.questions[id], moved++);
		if (moved) this.save();
		return moved;
	}

	openLesson(id: string) {
		this.state.lessons[id] = { openedAt: new Date().toISOString() };
		this.save();
	}

	addExamAttempt(a: ExamAttempt) {
		this.state.exams.push(a);
		this.save();
	}

	/** Câu cần ôn: sai ở lần đầu và chưa làm lại đúng trong mục Ôn câu sai. */
	needsReview(id: string) {
		const r = this.state.questions[id];
		return !!r && r.firstTry === false && !r.reviewedOk;
	}

	markReviewed(id: string, ok: boolean) {
		const r = this.state.questions[id];
		if (r && ok) {
			r.reviewedOk = true;
			this.save();
		}
	}

	status(lessonId: string): LessonStatus {
		const l = Study.lessons[lessonId];
		if (!l) return 'chua-soan';
		const qs = l.questions ?? [];
		if (!qs.length) return this.state.lessons[lessonId] ? 'xong' : 'chua-hoc';
		if (!qs.some((q) => this.q(q.id))) return 'chua-hoc';
		return qs.every((q) => this.q(q.id)?.correct) ? 'xong' : 'dang-hoc';
	}

	reviewCount() {
		return Study.lessonQuestions().filter((q) => this.needsReview(q.id)).length;
	}

	courseStats(courseId: string) {
		const es = Study.entries(courseId);
		const qs = es.flatMap((e) => Study.lessons[e.id]?.questions ?? []);
		const recs = qs.map((q) => this.q(q.id)).filter((r): r is QuestionRecord => !!r);
		return {
			total: es.length,
			written: es.filter((e) => Study.lessons[e.id]).length,
			done: es.filter((e) => this.status(e.id) === 'xong').length,
			answered: recs.length,
			firstOk: recs.filter((r) => r.firstTry).length,
		};
	}

	/** Bài kế tiếp: theo thứ tự ưu tiên môn, bài đầu tiên đã soạn mà chưa xong. */
	nextLesson() {
		const order = Study.manifest.priority ?? Study.manifest.courses.map((c) => c.id);
		for (const cid of order) {
			const e = Study.entries(cid).find((x) => Study.lessons[x.id] && this.status(x.id) !== 'xong');
			if (e) return e;
		}
	}
}

/** First run with the review schedule: start it from past answers (wrong first try -> due today). */
function seedSrs(s: ProgressState) {
	for (const [id, r] of Object.entries(s.questions)) {
		const fp = Study.question(id)?.fp;
		if (!fp || s.srs[fp]) continue;
		const ok = r.firstTry || r.reviewedOk;
		const day = (r.at ?? '').slice(0, 10) || today();
		s.srs[fp] = { box: ok ? 2 : 1, due: ok ? today(INTERVAL[2]) : today(), last: day, seen: r.tries ?? 1, wrong: r.firstTry ? 0 : 1 };
	}
}

function readLocal(): ProgressState {
	try {
		return normalize(JSON.parse(localStorage.getItem(KEY) ?? 'null'));
	} catch {
		return empty();
	}
}
function writeLocal(s: ProgressState) {
	try {
		localStorage.setItem(KEY, JSON.stringify(s));
	} catch {
		/* bộ nhớ trình duyệt không dùng được: bỏ qua */
	}
}

export const Progress = new ProgressStore();
