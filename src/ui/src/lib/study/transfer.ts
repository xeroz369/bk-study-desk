// Tạo · nhập · xuất gói luyện tập theo đúng chỗ (môn → chương → bài).
// Gói tự định vị bằng tên: môn theo course.code, chương theo title của unit, bài theo title của lesson (so sánh đã chuẩn hóa).
// Vì vậy nhập ở đâu thì đổi tên chương/bài trong gói cho khớp chỗ đó; xuất thì giữ nguyên tên để máy khác nhập vào đúng chỗ.

import { api } from '$lib/api/client';
import { packQuestion, Study } from './registry.svelte';
import { PACK_FORMAT, validatePack, type PackLesson, type PackQuestion, type PackReport, type StudyPack } from './pack';
import type { Course, Question } from './types';
import { detectFormat, parseAiken, parseGift, parseMoodleXml } from './formats';
import { parseMarkdown, writeMarkdown } from './markdown';
import { strFromU8, strToU8, unzipSync, zipSync } from 'fflate';
import { fingerprint } from './fingerprint';
import { normText } from './text';
import { Progress } from './progress.svelte';
import { APP_NAME } from '$lib/app-name';
import { isLmsQuizLesson, isRecallLesson, RECALL_PREFIX } from './lmsquiz';

/** Chỗ đang thao tác: cả môn, một chương (unit) hoặc một bài. */
export interface Scope {
	course: Course;
	unit?: number;
	lessonId?: string;
}

const norm = normText;

/** "Phương trình phi tuyến" → "phuong-trinh-phi-tuyen" (id gói/bài: chữ thường, số, gạch ngang). */
export function slug(s: string, fallback = 'muc') {
	const out = s
		.normalize('NFD')
		.replace(/[̀-ͯ]/g, '')
		.replace(/[đĐ]/g, 'd')
		.toLowerCase()
		.replace(/[^a-z0-9]+/g, '-')
		.replace(/^-+|-+$/g, '')
		.slice(0, 48);
	return out.length >= 2 ? out : fallback;
}

/** "ppt/0/ppt-02" → Scope. */
export function parseScope(path: string): Scope | null {
	const [cid, u, lid] = path.split('/');
	const course = Study.course(cid ?? '');
	if (!course) return null;
	let unit = u !== undefined && u !== '' && course.units[+u] ? +u : undefined;
	let lessonId = unit !== undefined && lid && course.units[unit].lessons.some((e) => e.id === lid) ? lid : undefined;
	// Chỉ số chương lệch (thứ tự gói đổi sau khi lưu) nhưng bài vẫn có: tìm lại chương theo id bài.
	if (lid && !lessonId) {
		const i = course.units.findIndex((x) => x.lessons.some((e) => e.id === lid));
		if (i >= 0) [unit, lessonId] = [i, lid];
	}
	return { course, unit, lessonId };
}
export const scopePath = (s: Scope) => [s.course.id, s.unit ?? '', s.lessonId ?? ''].join('/').replace(/\/+$/, '');
export const unitOf = (s: Scope) => (s.unit !== undefined ? s.course.units[s.unit] : undefined);
export const lessonOf = (s: Scope) => unitOf(s)?.lessons.find((e) => e.id === s.lessonId);
export function scopeLabel(s: Scope) {
	return lessonOf(s)?.title ?? unitOf(s)?.title ?? s.course.name;
}
/** Chỗ của một bài trong sổ (dùng cho nút ở trang bài). */
export function scopeOfLesson(lessonId: string): Scope | null {
	for (const course of Study.manifest.courses)
		for (const [i, u] of course.units.entries()) if (u.lessons.some((e) => e.id === lessonId)) return { course, unit: i, lessonId };
	return null;
}

// ------------------------------------------------------------------ xuất

/** Câu trong sổ → câu trong gói, giữ đúng loại. */
/** Dòng "Lần làm trên LMS: bạn chọn…" là đáp án riêng của người gửi: không đưa vào file chia sẻ. */
const OWN_ATTEMPT = /<p>\s*<small>\s*Lần làm trên LMS:[\s\S]*?<\/p>/g;

const toPackQ = (q: Question, i: number): PackQuestion => {
	const base = {
		id: `q${i + 1}`,
		...(q.tag ? { tag: q.tag } : {}),
		...(q.group ? { group: q.group } : {}),
		prompt: q.prompt,
		solution: q.solution?.replace(OWN_ATTEMPT, ''),
	};
	switch (q.type ?? 'single') {
		case 'multi':
			return { ...base, type: 'multi', options: [...q.options], answers: [...(q.answers ?? [])] };
		case 'numeric':
			return {
				...base,
				type: 'numeric',
				answer: q.value ?? 0,
				...(q.tolerance ? { tolerance: q.tolerance } : {}),
				...(q.unit ? { unit: q.unit } : {}),
			};
		case 'short':
			return { ...base, type: 'short', accept: [...(q.accept ?? [])] };
		default:
			return { ...base, options: [...q.options], answer: q.answer };
	}
};

/** Saved LMS quiz still open for others: never exported (shared only after it closes). */
/** A saved LMS quiz is shared only after it has closed for everyone. */
/** Quiz LMS chưa đóng: không xuất. Câu "Ghi lại" từ trí nhớ (gói quiz-lms-ghi-) cũng khóa theo quiz gốc. */
export const quizLocked = (lessonId: string) => {
	const recall = lessonId.startsWith(RECALL_PREFIX) ? Study.entries().find((e) => e.id === lessonId)?.title : undefined;
	return Study.quizInfo.some((i) => !i.shareable && (i.lessonId === lessonId || recall === `Ghi lại: ${i.quiz.quiz}`));
};

/** Tạo, Nhập, Xuất for one lesson: a saved LMS quiz takes no new questions (except one recalled from memory)
 *  and is shared only after it closes. */
export function lessonCan(lessonId: string) {
	const lms = isLmsQuizLesson(lessonId);
	return { tao: !lms || isRecallLesson(lessonId), nhap: !lms, xuat: !quizLocked(lessonId) };
}

/** A pack as it may be shared: recalled questions of an LMS quiz still open are left out (same rule as quizLocked). */
export function shareablePack(p: StudyPack): StudyPack | null {
	if (!p.id.startsWith(RECALL_PREFIX)) return p;
	const open = new Set(Study.quizInfo.filter((i) => !i.shareable).map((i) => `Ghi lại: ${i.quiz.quiz}`));
	const units = p.units.map((u) => ({ ...u, lessons: u.lessons.filter((l) => !open.has(l.title)) })).filter((u) => u.lessons.length);
	return units.length ? { ...p, units } : null;
}

/** One question as Study Markdown (for AI prompts: similar / explain). */
export function questionMarkdown(q: Question): string {
	const p: StudyPack = {
		format: PACK_FORMAT,
		id: 'x1',
		title: 'x',
		version: '1.0.0',
		course: { code: 'X', name: 'x' },
		authors: [{ name: 'x' }],
		units: [{ title: 'x', lessons: [{ id: 'x1', title: 'x', questions: [toPackQ(q, 0)] }] }],
	};
	const md = writeMarkdown(p).md;
	return md.slice(md.indexOf('### ')).trim();
}

/** Dựng gói từ phần sổ trong scope. Tên chương/bài giữ nguyên để người nhận nhập vào đúng chỗ. */
export function buildExport(s: Scope, author: string): StudyPack {
	const c = s.course;
	const code = c.code ?? c.id.toUpperCase();
	const units = c.units
		.map((u, ui) => ({ u, ui }))
		.filter(({ ui }) => s.unit === undefined || ui === s.unit)
		.map(({ u }) => {
			const used = new Set<string>();
			const lessons: PackLesson[] = u.lessons
				.filter((e) => (!s.lessonId || e.id === s.lessonId) && !quizLocked(e.id))
				.flatMap((e) => {
					const l = Study.lessons[e.id];
					if (!l) return [];
					let id = slug(e.title, 'bai');
					while (used.has(id)) id += '-2';
					used.add(id);
					const sources = (l.sources ?? e.sources ?? []).map((x) => ({ title: x.file, ...(x.pages ? { pages: x.pages } : {}) }));
					return [
						{
							id,
							title: e.title,
							...(sources.length ? { sources } : {}),
							...(l.sections?.length
								? { sections: l.sections.map((x) => ({ ...(x.title ? { title: x.title } : {}), body: x.html.trim() })) }
								: {}),
							questions: (l.questions ?? []).map(toPackQ),
						},
					];
				});
			return { title: u.title, lessons };
		})
		.filter((u) => u.lessons.length);
	const exams =
		s.unit === undefined
			? Object.values(Study.exams)
					.filter((x) => x.courseId === c.id && !x.id.startsWith('ngau-nhien-')) // random exams live only in memory
					.map((x) => ({
						id: slug(x.id, 'de'),
						title: x.title,
						minutes: x.minutes,
						...((x.scoring ?? c.scoring) ? { scoring: x.scoring ?? c.scoring } : {}),
						questions: Study.examQuestions(x)
							.filter((q) => !quizLocked(q.lessonId ?? ''))
							.map(toPackQ),
					}))
					.filter((x) => x.questions.length)
			: [];
	const label = scopeLabel(s);
	return {
		format: PACK_FORMAT,
		id: slug(`hcmut-${code}-${s.unit === undefined ? 'tron-mon' : label}`).slice(0, 64),
		title: s.unit === undefined ? c.name : `${c.name}: ${label}`,
		version: '1.0.0',
		language: 'vi',
		course: { code, name: c.name, school: 'HCMUT' },
		authors: [{ name: author || 'Không tên' }],
		license: 'CC-BY-SA-4.0',
		createdWith: `${APP_NAME} (xuất từ app)`,
		verified: { method: 'xem lời giải từng câu' },
		units,
		...(exams.length ? { exams } : {}),
	};
}

/** Lưu ra file (WebView2 hiện hộp thoại tải về / lưu). */
export function downloadText(name: string, text: string, mime: string) {
	const a = document.createElement('a');
	a.href = URL.createObjectURL(new Blob([text], { type: mime }));
	a.download = name;
	a.click();
	setTimeout(() => URL.revokeObjectURL(a.href), 10_000);
}
export const download = (pack: StudyPack) =>
	downloadText(`${pack.id}.studypack.json`, JSON.stringify(pack, null, 2) + '\n', 'application/json');

const MIME: Record<string, string> = { png: 'image/png', jpg: 'image/jpeg', jpeg: 'image/jpeg', gif: 'image/gif', webp: 'image/webp' };
const toB64 = (u: Uint8Array) => {
	let s = '';
	for (let i = 0; i < u.length; i += 0x8000) s += String.fromCharCode(...u.subarray(i, i + 0x8000));
	return btoa(s);
};
const fromB64 = (b: string) => Uint8Array.from(atob(b), (c) => c.charCodeAt(0));

/** Read a picked file: .zip (goi.md + img/) or any text file (.md, .json, .xml, .txt). */
/** Limits for imported files: a small zip must not inflate into gigabytes (zip bomb). */
const MAX_FILE = 25 * 1024 * 1024;
const MAX_UNZIPPED = 60 * 1024 * 1024;
const MAX_ENTRIES = 500;

export async function readPackFile(f: File): Promise<{ text: string; images: Record<string, string> }> {
	if (f.size > MAX_FILE) throw new Error(`File quá lớn (tối đa ${MAX_FILE / 1024 / 1024} MB).`);
	if (!/\.zip$/i.test(f.name)) return { text: await f.text(), images: {} };
	const images: Record<string, string> = {};
	let text = '';
	let total = 0;
	let count = 0;
	const entries = unzipSync(new Uint8Array(await f.arrayBuffer()), {
		filter: (file) => {
			total += file.originalSize;
			if (++count > MAX_ENTRIES || total > MAX_UNZIPPED) throw new Error('File zip quá lớn hoặc quá nhiều file.');
			return true;
		},
	});
	for (const [path, data] of Object.entries(entries)) {
		const ext = path.split('.').pop()?.toLowerCase() ?? '';
		if (/\.(md|json|xml|txt|gift)$/i.test(path) && !text) text = strFromU8(data);
		else if (MIME[ext]) images[path.replace(/^[^/]+\/(img\/)/, '$1')] = `data:${MIME[ext]};base64,${toB64(data)}`;
	}
	return { text, images };
}

/** Save a pack as Study Markdown: .md when there are no images, .zip (goi.md + img/) otherwise. */
export function exportMarkdown(p: StudyPack) {
	const { md, images } = writeMarkdown(p);
	if (!Object.keys(images).length) return downloadText(`${p.id}.md`, md, 'text/markdown');
	const files: Record<string, Uint8Array> = { 'goi.md': strToU8(md) };
	for (const [path, uri] of Object.entries(images)) files[path] = fromB64(uri.split(',')[1]);
	const a = document.createElement('a');
	a.href = URL.createObjectURL(new Blob([zipSync(files, { level: 6 })], { type: 'application/zip' }));
	a.download = `${p.id}.zip`;
	a.click();
	setTimeout(() => URL.revokeObjectURL(a.href), 10_000);
}

// ------------------------------------------------------------------ nhập

export interface Prepared {
	pack?: StudyPack;
	report?: PackReport;
	error?: string;
	notes: string[];
}

/**
 * Đọc gói (JSON) và đặt vào scope: nhập ở bài thì mọi câu vào bài đó, ở chương thì mọi bài vào chương đó,
 * ở môn thì giữ chương/bài của gói (trùng tên thì ghép, khác thì thêm mới). Gói môn khác thì đổi mã cho khớp môn đang nhập.
 */
export function prepareImport(raw: string, s: Scope | null, author = '', images: Record<string, string> = {}): Prepared {
	const r = prepareRaw(raw, s, author, images);
	// Gói nhận từ người khác mang id dành riêng (câu tự soạn, quiz LMS đã lưu trên máy này) sẽ ghi đè lên dữ liệu của mình:
	// đổi sang id riêng để nhập thành một gói mới.
	if (r.pack && RESERVED_IDS.some((x) => r.pack!.id.startsWith(x))) {
		const id = ('nhan-' + r.pack.id).slice(0, 64);
		r.pack = { ...r.pack, id };
		r.notes = [...r.notes.filter((n) => !/^(Cài lại|Cập nhật) gói/.test(n)), ...updateNotes(r.pack)];
		r.report = validatePack(r.pack, raw.length);
	}
	return r;
}

/** Tiền tố id gói do app tự tạo trên máy này (authoredPack, Kho quiz LMS): không nhận từ file bên ngoài. */
const RESERVED_IDS = ['tu-soan-', 'quiz-lms-'];

function prepareRaw(raw: string, s: Scope | null, author: string, images: Record<string, string>): Prepared {
	const notes: string[] = [];
	raw = raw
		.trim()
		.replace(/^```[\w-]*[ \t]*\n/, '')
		.replace(/\n```\s*$/, ''); // AI often wraps the answer in a code fence
	let data: unknown;
	const fmt = detectFormat(raw);
	if (fmt === 'markdown') {
		// Only bare questions (###) pasted while standing on a chapter or lesson: add "# chapter / ## lesson" for that place.
		let shift = 0;
		const unit = s ? unitOf(s) : undefined;
		if (s && unit && !/^#{1,2} /m.test(raw)) {
			const head = `# ${unit.title}\n\n## ${lessonOf(s)?.title ?? 'Câu nhập thêm'}\n\n`;
			const fm = /^---\n[\s\S]*?\n---\n/.exec(raw)?.[0] ?? '';
			raw = fm + head + raw.slice(fm.length);
			shift = head.split('\n').length - 1;
		}
		const r = parseMarkdown(raw, images);
		if (r.errors.length)
			return {
				error: `${r.errors.length} lỗi trong file Markdown`,
				notes: [
					...r.errors.slice(0, 8).map((e) => (e.line ? `dòng ${Math.max(1, e.line - shift)}: ` : '') + e.message),
					...(!unit && !/^#{1,2} /m.test(raw) ? ['Mẹo: chọn một chương hoặc bài rồi dán, app tự đặt câu vào đó.'] : []),
				],
			};
		notes.push(...r.warnings.slice(0, 6).map((w) => `cảnh báo${w.line ? ` dòng ${w.line}` : ''}: ${w.message}`));
		const p = r.pack;
		if (!p.course.code && s) p.course = { code: s.course.code ?? s.course.id.toUpperCase(), name: s.course.name, school: 'HCMUT' };
		if (!p.authors.length) p.authors = [{ name: author || 'Không tên' }];
		if (!p.course.code) return { error: 'File chưa ghi môn (dòng "mon: MT1009 ..." ở phần đầu) và chưa chọn môn để nhập vào.', notes };
		data = p;
	} else if (fmt && fmt !== 'studypack') {
		// Aiken / GIFT / Moodle XML: đổi sang gói rồi xử lý như gói thường (đặt vào đúng chỗ bên dưới).
		if (!s) return { error: 'Chọn môn (hoặc chương, bài) trước khi nhập Aiken, GIFT hay Moodle XML.', notes };
		const label = { aiken: 'Aiken', gift: 'GIFT', moodlexml: 'Moodle XML' }[fmt];
		const conv = fmt === 'aiken' ? parseAiken(raw) : fmt === 'gift' ? parseGift(raw) : parseMoodleXml(raw);
		notes.push(`Đọc file ${label}: ${conv.lessons.reduce((n, l) => n + (l.questions?.length ?? 0), 0)} câu.`, ...conv.notes);
		const code = s.course.code ?? s.course.id.toUpperCase();
		data = {
			format: PACK_FORMAT,
			id: slug(`nhap-${code}-${label}-${Date.now().toString(36)}`),
			title: `${s.course.name}: nhập từ ${label}`,
			version: '1.0.0',
			language: 'vi',
			course: { code, name: s.course.name, school: 'HCMUT' },
			authors: [{ name: author || 'Nhập từ file' }],
			license: 'CC-BY-SA-4.0',
			createdWith: `Nhập từ ${label}`,
			verified: { method: `theo đáp án trong file ${label} gốc` },
			units: [{ title: unitOf(s)?.title ?? `Câu nhập từ ${label}`, lessons: conv.lessons.filter((l) => l.questions?.length) }],
		} satisfies StudyPack;
		if (!(data as StudyPack).units[0].lessons.length) return { error: `Không đọc được câu nào từ file ${label}.`, notes };
	} else
		try {
			data = JSON.parse(raw.trim().replace(/^```(?:json)?\s*|\s*```$/g, '')); // AI hay bọc trong ```json … ```
		} catch (e) {
			return {
				error:
					'Không nhận ra định dạng. Hỗ trợ: gói .studypack.json, Moodle XML, GIFT, Aiken. (JSON lỗi: ' +
					(e instanceof Error ? e.message : String(e)) +
					')',
				notes,
			};
		}
	const first = validatePack(data, raw.length);
	if (!first.ok || !s) return { pack: first.ok ? (data as StudyPack) : undefined, report: first, notes };
	const p = structuredClone(data) as StudyPack;
	const code = s.course.code ?? s.course.id.toUpperCase();
	if (norm(p.course.code) !== norm(code)) {
		notes.push(`Gói ghi môn ${p.course.code} (${p.course.name}), sẽ nhập vào ${s.course.name}.`);
		p.course = { ...p.course, code, name: s.course.name };
	}
	const unit = unitOf(s);
	const lesson = lessonOf(s);
	if (unit && lesson) {
		// Một bài: gộp kiến thức + câu của mọi bài trong gói, đánh lại id câu.
		const all = p.units.flatMap((u) => u.lessons);
		const questions = all.flatMap((l) => l.questions ?? []).map((q, i) => ({ ...q, id: `q${i + 1}` }));
		const sections = all.flatMap((l) => l.sections ?? []);
		p.units = [
			{
				title: unit.title,
				lessons: [{ id: slug(lesson.title, 'bai'), title: lesson.title, ...(sections.length ? { sections } : {}), questions }],
			},
		];
		if (p.exams?.length) notes.push('Đề thi thử trong gói được bỏ qua khi nhập vào một bài.');
		delete p.exams;
		notes.push(`${questions.length} câu sẽ thêm vào bài "${lesson.title}".`);
	} else if (unit) {
		const used = new Set<string>();
		const lessons = p.units
			.flatMap((u) => u.lessons)
			.map((l) => {
				let id = l.id;
				while (used.has(id)) id += '-2';
				used.add(id);
				return { ...l, id };
			});
		// Exam refs point at "<lesson id>.<question id>"; lesson ids may repeat across units, so keep the first match only.
		const oldIds = new Map<string, string>();
		p.units.flatMap((u) => u.lessons).forEach((l, i) => !oldIds.has(l.id) && oldIds.set(l.id, lessons[i].id));
		p.units = [{ title: unit.title, lessons }];
		p.exams = p.exams?.map((x) => ({
			...x,
			questionRefs: x.questionRefs?.map((r) => {
				const [l, q] = r.split('.');
				return `${oldIds.get(l) ?? l}.${q}`;
			}),
		}));
		notes.push(`${lessons.length} bài sẽ vào chương "${unit.title}" (bài trùng tên thì thêm câu vào bài đó).`);
	} else {
		const hit = p.units.filter((u) => s.course.units.some((x) => norm(x.title) === norm(u.title))).length;
		notes.push(`${p.units.length} chương: ${hit} ghép vào chương cùng tên, ${p.units.length - hit} thêm mới.`);
	}
	notes.push(...updateNotes(p));
	return { pack: p, report: validatePack(p, raw.length), notes };
}

// ------------------------------------------------------------------ cập nhật gói, câu trùng

/** Fingerprint of a pack question exactly as the registry computes it after loading (same mapping + sanitizing). */
const packFp = (q: PackQuestion) => fingerprint(packQuestion(q, '', ''));

/** fingerprint -> question id the registry will give it (registry.addPack: "<gói>.<bài>.<câu>"). */
function newIds(p: StudyPack): Map<string, string> {
	const out = new Map<string, string>();
	for (const u of p.units)
		for (const l of u.lessons) (l.questions ?? []).forEach((q, i) => out.set(packFp(q), `${p.id}.${l.id}.${q.id ?? 'q' + (i + 1)}`));
	for (const x of p.exams ?? []) (x.questions ?? []).forEach((q, i) => out.set(packFp(q), `${p.id}.${x.id}.${q.id ?? 'q' + (i + 1)}`));
	return out;
}

/** Notes for the import preview: updating an installed pack, questions already in the book. */
function updateNotes(p: StudyPack): string[] {
	const notes: string[] = [];
	const old = Study.packs.find((x) => x.pack?.id === p.id)?.pack;
	if (old)
		notes.push(
			(old.version === p.version ? `Cài lại gói đã có (v${p.version})` : `Cập nhật gói từ v${old.version} lên v${p.version}`) +
				': kết quả làm bài, lịch ôn và ghi chú được giữ.',
		);
	const have = new Set(
		[...Study.lessonQuestions(), ...Object.values(Study.exams).flatMap((x) => x.questions ?? [])]
			.filter((q) => q.packId !== p.id)
			.map((q) => q.fp),
	);
	const dup = [...newIds(p).keys()].filter((fp) => have.has(fp)).length;
	if (dup) notes.push(`${dup} câu trùng với câu đã có, chỉ hiện một lần.`);
	return notes;
}

/** Lưu gói vào app rồi tải lại trang. Lỗi (server từ chối, mất kết nối) ném ra cho trang báo; chưa lưu xong thì không đổi địa chỉ.
 *  hashAfter: địa chỉ mở sau khi tải lại (vd. bài vừa tạo). */
export async function install(p: StudyPack, hashAfter?: string) {
	// Updating a pack: question ids may change (renumbered, moved); carry results over by fingerprint.
	const ids = newIds(p);
	const old = [...Study.lessonQuestions(), ...Object.values(Study.exams).flatMap((x) => x.questions ?? [])].filter(
		(q) => q.packId === p.id,
	);
	const moves = new Map<string, string>();
	for (const q of old) if (q.fp && ids.has(q.fp)) moves.set(q.id, ids.get(q.fp)!);
	// Ids the new version still has keep their results (e.g. a typo fix changes the fingerprint, not the id).
	const still = new Set(ids.values());
	const res = await api.importPack(p);
	if (!res.ok) throw new Error('App không lưu được gói');
	Progress.migrateIds(
		moves,
		old.map((q) => q.id).filter((id) => !still.has(id)),
	);
	await Progress.flush(); // also any answer still waiting for the debounced save
	sessionStorage.setItem('studypack.flash', `Đã lưu gói "${p.title}"`);
	if (hashAfter) history.replaceState(null, '', hashAfter);
	location.reload();
}

// ------------------------------------------------------------------ tự soạn

/** Gói chứa câu/bài tự soạn của môn: tu-soan-<mã môn>, lưu trong content/packs như gói khác. */
export function authoredPack(c: Course, author: string, recall = false): StudyPack {
	const code = c.code ?? c.id.toUpperCase();
	// recall: questions written from memory for an LMS quiz with no review → their own pack (quiz-lms-ghi-<code>).
	const id = slug(`${recall ? RECALL_PREFIX : 'tu-soan-'}${code}`);
	const have = Study.packs.find((x) => x.pack?.id === id)?.pack;
	const p: StudyPack = have
		? (JSON.parse(JSON.stringify(have)) as StudyPack)
		: {
				format: PACK_FORMAT,
				id,
				title: `${c.name}: câu tự tạo`,
				version: '1.0.0',
				language: 'vi',
				course: { code, name: c.name, school: 'HCMUT' },
				authors: [{ name: author || 'Không tên' }],
				license: 'CC-BY-SA-4.0',
				createdWith: `${APP_NAME} (tự tạo)`,
				verified: { method: 'người tạo tự kiểm' },
				units: [],
			};
	if (author && !p.authors.some((a) => a.name === author)) p.authors.push({ name: author });
	return p;
}

/** Bài (theo tên) trong chương (theo tên) của gói tự soạn; chưa có thì tạo. */
export function lessonIn(p: StudyPack, unitTitle: string, lessonTitle: string): PackLesson {
	let u = p.units.find((x) => norm(x.title) === norm(unitTitle));
	if (!u) p.units.push((u = { title: unitTitle, lessons: [] }));
	let l = u.lessons.find((x) => norm(x.title) === norm(lessonTitle));
	if (!l) {
		const ids = new Set(p.units.flatMap((x) => x.lessons.map((y) => y.id)));
		let id = slug(lessonTitle, 'bai');
		while (ids.has(id)) id += '-2';
		u.lessons.push((l = { id, title: lessonTitle, questions: [] }));
	}
	return l;
}

export function bumpVersion(p: StudyPack) {
	const [a, b, c] = p.version.split('.').map(Number);
	p.version = `${a}.${b}.${c + 1}`;
}
