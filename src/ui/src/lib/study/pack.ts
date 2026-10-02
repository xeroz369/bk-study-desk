// Study Pack v1: định dạng gói luyện tập dùng chung (JSON thuần, không chứa code).
// Ai (người hay AI) cũng soạn được theo spec ở studypack/SPEC.md; app và CLI (studypack/validate.ts) dùng chung validator này.
// File chỉ dùng cú pháp TS "xóa được" (không enum, không namespace) để Node 24 chạy thẳng bằng type stripping.

export const PACK_FORMAT = 'studypack/1';

export interface PackSource {
	title: string;
	pages?: string;
	url?: string;
}
/** Loại câu, tương ứng quiz Moodle: multichoice (single/multi), truefalse, numerical, shortanswer. */
export const QUESTION_TYPES = ['single', 'multi', 'truefalse', 'numeric', 'short'] as const;
export type PackQuestionType = (typeof QUESTION_TYPES)[number];

export interface PackQuestion {
	id?: string;
	/** mặc định "single" */
	type?: PackQuestionType;
	tag?: string;
	prompt: string;
	/** single, multi: các phương án (HTML, có thể có ảnh) */
	options?: string[];
	/** single: chỉ số đúng (từ 0) · truefalse: true/false · numeric: đáp số */
	answer?: number | boolean;
	/** multi: các chỉ số đúng */
	answers?: number[];
	/** numeric: sai số tuyệt đối cho phép (mặc định 0), đơn vị hiển thị */
	tolerance?: number;
	unit?: string;
	/** short: các đáp án chữ được chấp nhận (không phân biệt hoa thường, khoảng trắng) */
	accept?: string[];
	/** không xáo phương án của câu này (vd. có "Cả A và B đều đúng") */
	keepOrder?: boolean;
	solution: string;
	difficulty?: 1 | 2 | 3;
	skills?: string[];
	source?: PackSource;
}
export interface PackLesson {
	id: string;
	title: string;
	sources?: PackSource[];
	sections?: { title?: string; body: string }[];
	questions?: PackQuestion[];
}
export interface PackExam {
	id: string;
	title: string;
	minutes: number;
	scoring?: { count: number; right: number; wrong: number };
	questionRefs?: string[];
	questions?: PackQuestion[];
}
export interface StudyPack {
	format: typeof PACK_FORMAT;
	id: string;
	title: string;
	version: string;
	language?: string;
	course: { code: string; name: string; school?: string };
	description?: string;
	authors: { name: string; contact?: string }[];
	license?: string;
	createdWith?: string;
	verified?: { method: string; by?: string; at?: string };
	sources?: PackSource[];
	/** xáo câu / xáo phương án mỗi lần làm (người học vẫn tắt được trong app) */
	settings?: { shuffleQuestions?: boolean; shuffleOptions?: boolean };
	units: { title: string; lessons: PackLesson[] }[];
	exams?: PackExam[];
}

export interface PackIssue {
	path: string;
	message: string;
}
export interface PackReport {
	ok: boolean;
	errors: PackIssue[];
	warnings: PackIssue[];
	stats: { lessons: number; questions: number; exams: number };
}

const ID = /^[a-z0-9][a-z0-9-]{1,63}$/;
const VERSION = /^\d+\.\d+\.\d+$/;
// Thẻ HTML được phép trong nội dung (app lọc lại lần nữa khi hiển thị, xem sanitize.ts).
export const ALLOWED_TAGS = new Set([
	'p',
	'br',
	'b',
	'strong',
	'i',
	'em',
	'u',
	'sub',
	'sup',
	'code',
	'pre',
	'span',
	'div',
	'ul',
	'ol',
	'li',
	'table',
	'thead',
	'tbody',
	'tr',
	'th',
	'td',
	'hr',
	'blockquote',
	'small',
	'mark',
	'img',
]);
/** Ảnh chỉ nhận dạng nhúng sẵn (data URI raster): gói tự chứa, không tải gì từ mạng, không SVG (có thể chứa script). */
export const IMG_SRC = /^data:image\/(png|jpeg|gif|webp);base64,[A-Za-z0-9+/=\s]+$/;
// class được giữ lại (khớp CSS của app): công thức, cảnh báo, phím bấm máy tính, mẹo.
export const ALLOWED_CLASSES = new Set(['math', 'warn', 'keys', 'tip', 'note']);
// Gói có ảnh nhúng (base64) nên cho rộng; mỗi ảnh nên dưới ~1 MB (app tự nén khi chèn ảnh lúc soạn).
const MAX_BYTES = 20 * 1024 * 1024;
const MAX_IMAGE_CHARS = 1.5 * 1024 * 1024;

/** Kiểm tra một gói (đã parse JSON). Lỗi = app không nạp; cảnh báo = vẫn nạp nhưng nên sửa. */
export function validatePack(input: unknown, rawSize = 0): PackReport {
	const errors: PackIssue[] = [];
	const warnings: PackIssue[] = [];
	const err = (path: string, message: string) => errors.push({ path, message });
	const warn = (path: string, message: string) => warnings.push({ path, message });
	const stats = { lessons: 0, questions: 0, exams: 0 };
	const isObj = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v);
	const str = (v: unknown, path: string, required = true): v is string => {
		if (typeof v === 'string' && v.trim()) return true;
		if (required || v !== undefined) err(path, 'cần chuỗi không rỗng');
		return false;
	};

	if (rawSize > MAX_BYTES) err('', `gói quá lớn (${Math.round(rawSize / 1024)} KB, tối đa ${MAX_BYTES / 1024 / 1024} MB)`);
	if (!isObj(input)) return { ok: false, errors: [{ path: '', message: 'gói phải là một JSON object' }], warnings, stats };
	const p = input;

	if (p.format !== PACK_FORMAT) err('format', `phải là "${PACK_FORMAT}"`);
	if (!(typeof p.id === 'string' && ID.test(p.id))) err('id', 'chữ thường, số, gạch ngang; 2–64 ký tự (vd. "hcmut-mt1009-chia-doi")');
	str(p.title, 'title');
	if (!(typeof p.version === 'string' && VERSION.test(p.version))) err('version', 'dạng x.y.z (vd. "1.0.0")');
	if (!isObj(p.course)) err('course', 'cần { code, name }');
	else {
		str(p.course.code, 'course.code');
		str(p.course.name, 'course.name');
	}
	if (!Array.isArray(p.authors) || p.authors.length === 0) err('authors', 'cần ít nhất một tác giả { name }');
	else p.authors.forEach((a, i) => (isObj(a) ? str(a.name, `authors[${i}].name`) : err(`authors[${i}]`, 'cần { name }')));
	if (p.license === undefined) warn('license', 'nên ghi giấy phép (vd. "CC-BY-SA-4.0") để người khác biết được dùng lại thế nào');
	if (p.verified === undefined) warn('verified', 'chưa ghi đã kiểm đáp án bằng cách nào (python, máy tính, giải tay…)');

	const text = (v: unknown, path: string, required = true) => {
		if (!str(v, path, required)) return;
		checkText(v as string, path, err, warn);
	};
	const sources = (v: unknown, path: string) => {
		if (v === undefined) return;
		if (!Array.isArray(v)) return err(path, 'cần mảng { title, pages?, url? }');
		v.forEach((s, i) => (isObj(s) ? str(s.title, `${path}[${i}].title`) : err(`${path}[${i}]`, 'cần { title }')));
	};
	sources(p.sources, 'sources');

	const allQ = new Set<string>();
	const question = (q: unknown, path: string, owner: string, i: number) => {
		if (!isObj(q)) return err(path, 'câu hỏi phải là object');
		stats.questions++;
		const type = (q.type ?? 'single') as string;
		if (!(QUESTION_TYPES as readonly string[]).includes(type)) err(`${path}.type`, `loại câu: ${QUESTION_TYPES.join(', ')}`);
		const qid = q.id === undefined ? `q${i + 1}` : q.id;
		if (typeof qid !== 'string' || !ID.test(qid)) err(`${path}.id`, 'id câu: chữ thường, số, gạch ngang');
		else if (allQ.has(`${owner}.${qid}`)) err(`${path}.id`, `trùng id "${qid}"`);
		else allQ.add(`${owner}.${qid}`);
		text(q.prompt, `${path}.prompt`);
		text(q.solution, `${path}.solution`);
		if (q.tag !== undefined) str(q.tag, `${path}.tag`);
		if (type === 'single' || type === 'multi') {
			if (!Array.isArray(q.options) || q.options.length < 2 || q.options.length > 8) err(`${path}.options`, 'cần 2–8 phương án');
			else {
				const n = q.options.length;
				q.options.forEach((o, k) => text(o, `${path}.options[${k}]`));
				const norm = q.options.map((o) => String(o).replace(/\s+/g, ' ').trim());
				if (new Set(norm).size !== norm.length) warn(`${path}.options`, 'có phương án trùng nhau');
				const idx = (v: unknown) => Number.isInteger(v) && (v as number) >= 0 && (v as number) < n;
				if (type === 'single' && !idx(q.answer)) err(`${path}.answer`, `chỉ số đáp án đúng, đếm từ 0 (0–${n - 1})`);
				if (type === 'multi') {
					if (!Array.isArray(q.answers) || q.answers.length === 0 || !q.answers.every(idx))
						err(`${path}.answers`, `mảng chỉ số đúng, đếm từ 0 (0–${n - 1}), ít nhất 1`);
					else if (new Set(q.answers).size !== q.answers.length) err(`${path}.answers`, 'chỉ số bị lặp');
				}
			}
		} else if (type === 'truefalse') {
			if (typeof q.answer !== 'boolean') err(`${path}.answer`, 'true (Đúng) hoặc false (Sai)');
		} else if (type === 'numeric') {
			if (typeof q.answer !== 'number' || !Number.isFinite(q.answer)) err(`${path}.answer`, 'đáp số (number)');
			if (q.tolerance !== undefined && !(typeof q.tolerance === 'number' && q.tolerance >= 0))
				err(`${path}.tolerance`, 'sai số cho phép ≥ 0');
			if (q.tolerance === undefined && typeof q.answer === 'number' && !Number.isInteger(q.answer))
				warn(`${path}.tolerance`, 'đáp số lẻ mà không có sai số cho phép: người làm phải gõ đúng từng chữ số');
			if (q.unit !== undefined) str(q.unit, `${path}.unit`);
		} else if (type === 'short') {
			if (!Array.isArray(q.accept) || q.accept.length === 0 || !q.accept.every((x) => typeof x === 'string' && x.trim()))
				err(`${path}.accept`, 'mảng đáp án chữ được chấp nhận, ít nhất 1');
		}
		if (q.difficulty !== undefined && ![1, 2, 3].includes(q.difficulty as number)) err(`${path}.difficulty`, '1, 2 hoặc 3');
		if (q.source !== undefined) sources([q.source], `${path}.source`);
	};

	const lessonIds = new Set<string>();
	if (!Array.isArray(p.units) || p.units.length === 0) err('units', 'cần ít nhất một chương { title, lessons }');
	else
		p.units.forEach((u, ui) => {
			const up = `units[${ui}]`;
			if (!isObj(u)) return err(up, 'cần { title, lessons }');
			str(u.title, `${up}.title`);
			if (!Array.isArray(u.lessons) || u.lessons.length === 0) return err(`${up}.lessons`, 'cần ít nhất một bài');
			u.lessons.forEach((l, li) => {
				const lp = `${up}.lessons[${li}]`;
				if (!isObj(l)) return err(lp, 'bài học phải là object');
				stats.lessons++;
				if (!(typeof l.id === 'string' && ID.test(l.id))) err(`${lp}.id`, 'id bài: chữ thường, số, gạch ngang');
				else if (lessonIds.has(l.id)) err(`${lp}.id`, `trùng id bài "${l.id}"`);
				else lessonIds.add(l.id);
				str(l.title, `${lp}.title`);
				sources(l.sources, `${lp}.sources`);
				if (l.sections !== undefined)
					if (!Array.isArray(l.sections)) err(`${lp}.sections`, 'cần mảng { title?, body }');
					else
						l.sections.forEach((s, si) => {
							if (!isObj(s)) return err(`${lp}.sections[${si}]`, 'cần { title?, body }');
							if (s.title !== undefined) text(s.title, `${lp}.sections[${si}].title`);
							text(s.body, `${lp}.sections[${si}].body`);
						});
				if (l.questions !== undefined)
					if (!Array.isArray(l.questions)) err(`${lp}.questions`, 'cần mảng câu hỏi');
					else l.questions.forEach((q, qi) => question(q, `${lp}.questions[${qi}]`, String(l.id), qi));
				if (!l.sections && !l.questions) warn(lp, 'bài không có nội dung lẫn câu hỏi');
			});
		});

	if (p.exams !== undefined)
		if (!Array.isArray(p.exams)) err('exams', 'cần mảng đề');
		else
			p.exams.forEach((x, xi) => {
				const xp = `exams[${xi}]`;
				if (!isObj(x)) return err(xp, 'đề phải là object');
				stats.exams++;
				if (!(typeof x.id === 'string' && ID.test(x.id))) err(`${xp}.id`, 'id đề: chữ thường, số, gạch ngang');
				else if (lessonIds.has(x.id)) err(`${xp}.id`, 'id đề trùng id bài');
				str(x.title, `${xp}.title`);
				if (!(typeof x.minutes === 'number' && x.minutes > 0 && x.minutes <= 600)) err(`${xp}.minutes`, 'số phút 1–600');
				if (x.scoring !== undefined) {
					const s = x.scoring as Record<string, unknown>;
					if (!isObj(s) || typeof s.count !== 'number' || typeof s.right !== 'number' || typeof s.wrong !== 'number')
						err(`${xp}.scoring`, 'cần { count, right, wrong } (số)');
				}
				(Array.isArray(x.questions) ? x.questions : []).forEach((q, qi) => question(q, `${xp}.questions[${qi}]`, String(x.id), qi));
				if (x.questionRefs !== undefined)
					if (!Array.isArray(x.questionRefs)) err(`${xp}.questionRefs`, 'cần mảng "idBài.idCâu"');
					else
						x.questionRefs.forEach((r, ri) => {
							if (typeof r !== 'string' || !allQ.has(r)) err(`${xp}.questionRefs[${ri}]`, `không có câu "${String(r)}" trong gói`);
						});
				if (!x.questions && !x.questionRefs) err(xp, 'đề cần questions hoặc questionRefs');
			});

	if (stats.questions === 0) warn('', 'gói chưa có câu hỏi nào');
	return { ok: errors.length === 0, errors, warnings, stats };
}

/** Kiểm tra một đoạn nội dung: HTML lạ, script, lỗi escape TeX trong JSON. */
function checkText(s: string, path: string, err: (p: string, m: string) => void, warn: (p: string, m: string) => void) {
	if (/<\s*(script|style|iframe|object|embed|form|input|link|meta|svg|math|base)\b/i.test(s))
		err(path, 'không được có thẻ script/style/iframe/svg… (chỉ chữ, công thức TeX, HTML định dạng đơn giản, ảnh data URI)');
	for (const m of s.matchAll(/<img\b[^>]*>/gi)) {
		const src = m[0].match(/\ssrc\s*=\s*"([^"]*)"/i)?.[1] ?? '';
		if (!IMG_SRC.test(src)) {
			err(path, 'ảnh phải nhúng dạng data:image/png|jpeg|gif|webp;base64,… (không link ngoài, không SVG)');
			break;
		}
		if (src.length > MAX_IMAGE_CHARS) warn(path, `ảnh lớn (${Math.round(src.length / 1024)} KB): nên thu nhỏ còn dưới 1 MB`);
	}
	if (/\son\w+\s*=/i.test(s) || /javascript:/i.test(s)) err(path, 'không được có thuộc tính sự kiện (onclick…) hay javascript:');
	// JSON hiểu "\f", "\b", "\t", "\v" là ký tự điều khiển: dấu hiệu quên viết "\\frac", "\\beta", "\\times"…
	const ctl = s.match(/[\f\b\t\v\x00-\x08\x0e-\x1f]/);
	if (ctl)
		err(path, `có ký tự điều khiển (mã ${ctl[0].charCodeAt(0)}): trong JSON phải viết "\\\\frac", "\\\\beta", "\\\\times"… (hai dấu \\)`);
	// "\right", "\rho" → CR + "ight"; "\neq", "\nabla", "\nu" → xuống dòng + "eq": cũng là quên escape.
	if (/\r[a-z]/.test(s)) err(path, 'có "\\r" dính chữ (vd. "\\right", "\\rho" chưa escape): viết "\\\\right"');
	if (/\n(eq|abla|u\b|ot\b|ewline|exists|leq|geq|i\b|cong|parallel|mid\b)/.test(s))
		warn(path, 'có xuống dòng dính chữ (vd. "\\neq", "\\nabla" chưa escape?): kiểm tra lại "\\\\neq"');
	// Chỉ tính là thẻ khi sau "<" là chữ cái: "f(0)<0", "x<10" là toán, không phải thẻ.
	for (const m of s.matchAll(/<\/?([a-z][a-z0-9]*)\b/gi)) {
		if (!ALLOWED_TAGS.has(m[1].toLowerCase())) {
			warn(path, `thẻ <${m[1]}> không được hỗ trợ, app sẽ bỏ thẻ (giữ chữ)`);
			break;
		}
	}
	const opens = (s.match(/\\\(/g) ?? []).length,
		closes = (s.match(/\\\)/g) ?? []).length;
	if (opens !== closes) warn(path, `số "\\(" (${opens}) và "\\)" (${closes}) lệch nhau: công thức có thể hiện sai`);
	const dOpen = (s.match(/\\\[/g) ?? []).length,
		dClose = (s.match(/\\\]/g) ?? []).length;
	if (dOpen !== dClose) warn(path, `số "\\[" và "\\]" lệch nhau`);
}

/** Tổng số câu hỏi trong gói (dùng cho danh sách gói). */
export function countQuestions(p: StudyPack) {
	return (
		p.units.reduce((n, u) => n + u.lessons.reduce((m, l) => m + (l.questions?.length ?? 0), 0), 0) +
		(p.exams ?? []).reduce((n, x) => n + (x.questions?.length ?? 0), 0)
	);
}
