// Study Markdown: format to write and share packs by hand or with AI (spec: studypack/SPEC.md).
//   # chương · ## bài · ### câu · - [x] phương án · = đáp án · > lời giải · ![](img/a.webp) ảnh
// parseMarkdown: .md (+ images from the zip) -> StudyPack, errors carry line numbers.
// writeMarkdown: StudyPack -> .md + image files (data URIs pulled out into img/).
// Pure TS, no DOM: the CLI (studypack/validate.ts) uses it too.

import { PACK_FORMAT, validatePack, type PackIssue, type PackLesson, type PackQuestion, type StudyPack } from './pack.ts';

export interface MdIssue {
	line: number;
	message: string;
}
export interface MdResult {
	pack: StudyPack;
	errors: MdIssue[];
	warnings: MdIssue[];
}

const EXAM_UNIT = /^(đề thi thử|de thi thu|exams?)$/i;
const KEEP_ORDER = /^(giữ thứ tự|giu thu tu|keep order)$/i;
/** "nhóm: f1" trong dòng ###: các câu cùng nhóm trong bài dùng chung đề, luôn đi cùng nhau. */
const GROUP = /^(?:nhóm|nhom|group)\s*:\s*(.+)$/i;

const slug = (s: string, fallback: string) => {
	const out = s
		.normalize('NFD')
		.replace(/[̀-ͯ]/g, '')
		.replace(/[đĐ]/g, 'd')
		.toLowerCase()
		.replace(/[^a-z0-9]+/g, '-')
		.replace(/^-+|-+$/g, '')
		.slice(0, 48);
	return out.length >= 2 ? out : fallback;
};
const yes = (v?: string) => !!v && /^(co|có|yes|true|1|bật|bat)$/i.test(v.trim());

// ------------------------------------------------------------------ Markdown -> HTML (small subset)

/**
 * Render the Markdown subset used in packs. TeX (\( \), \[ \], $$ $$) and raw HTML pass through untouched;
 * images resolve through `img` (path -> data URI). The app still sanitizes the result before showing it.
 */
export function mdToHtml(md: string, img: (src: string) => string = (s) => s): string {
	const keep: string[] = [];
	const hold = (s: string) => `\u0000${keep.push(s) - 1}\u0000`;
	let t = md.replace(/\r/g, '');
	// Protect math and inline code first so * _ | inside them are not touched.
	t = t.replace(/\\\[[\s\S]*?\\\]|\$\$[\s\S]*?\$\$|\\\([\s\S]*?\\\)|`[^`\n]+`/g, (m) =>
		hold(m.startsWith('`') ? `<code>${escapeHtml(m.slice(1, -1))}</code>` : m),
	);
	const inline = (s: string) =>
		s
			.replace(/!\[([^\]]*)\]\(([^)\s]+)\)/g, (_m, alt, src) => hold(`<img src="${img(src)}" alt="${escapeAttr(alt)}">`))
			.replace(/\*\*([^*]+)\*\*/g, '<b>$1</b>')
			.replace(/(^|[^*\w])\*([^*\n]+)\*(?!\w)/g, '$1<i>$2</i>')
			.replace(/ {2,}$/gm, '<br>');
	const out: string[] = [];
	const lines = t.split('\n');
	for (let i = 0; i < lines.length;) {
		const line = lines[i];
		if (!line.trim()) {
			i++;
			continue;
		}
		// Table: header row + |---| row.
		if (/^\s*\|.*\|\s*$/.test(line) && /^\s*\|[\s:|-]+\|\s*$/.test(lines[i + 1] ?? '')) {
			const row = (r: string) =>
				r
					.trim()
					.replace(/^\||\|$/g, '')
					.split('|')
					.map((c) => inline(c.trim()));
			const head = row(line);
			i += 2;
			const body: string[][] = [];
			while (i < lines.length && /^\s*\|.*\|\s*$/.test(lines[i])) body.push(row(lines[i++]));
			out.push(
				`<table><thead><tr>${head.map((c) => `<th>${c}</th>`).join('')}</tr></thead><tbody>${body.map((r) => `<tr>${r.map((c) => `<td>${c}</td>`).join('')}</tr>`).join('')}</tbody></table>`,
			);
			continue;
		}
		// Lists.
		const ul = /^\s*[-*]\s+(?!\[[ xX]\])/,
			ol = /^\s*\d+[.)]\s+/;
		if (ul.test(line) || ol.test(line)) {
			const ordered = ol.test(line);
			const items: string[] = [];
			while (i < lines.length && (ordered ? ol : ul).test(lines[i])) items.push(inline(lines[i++].replace(ordered ? ol : ul, '')));
			out.push(`<${ordered ? 'ol' : 'ul'}>${items.map((x) => `<li>${x}</li>`).join('')}</${ordered ? 'ol' : 'ul'}>`);
			continue;
		}
		// Raw HTML block or display math stays as its own block.
		if (/^\s*<(div|table|ul|ol|p|pre|blockquote)\b/i.test(line) || /^\s*\u0000\d+\u0000\s*$/.test(line)) {
			const block: string[] = [];
			while (i < lines.length && lines[i].trim()) block.push(lines[i++]);
			const s = block.join('\n');
			out.push(/^\s*\u0000\d+\u0000\s*$/.test(s) ? `<div class="math">${s.trim()}</div>` : s);
			continue;
		}
		const para: string[] = [];
		while (i < lines.length && lines[i].trim() && !ul.test(lines[i]) && !ol.test(lines[i]) && !/^\s*\|.*\|\s*$/.test(lines[i]))
			para.push(lines[i++]);
		out.push(`<p>${inline(para.join('\n'))}</p>`);
	}
	return out.join('').replace(/\u0000(\d+)\u0000/g, (_m, k) => keep[+k]);
}

const escapeHtml = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const escapeAttr = (s: string) => escapeHtml(s).replace(/"/g, '&quot;');

// ------------------------------------------------------------------ parse

/**
 * Parse a Study Markdown file. `images` maps paths in the zip (img/a.webp) to data URIs;
 * a missing image is a warning (text still imports).
 */
export function parseMarkdown(text: string, images: Record<string, string> = {}): MdResult {
	const errors: MdIssue[] = [];
	const warnings: MdIssue[] = [];
	const lines = text.replace(/^﻿/, '').replace(/\r/g, '').split('\n');
	let n = 0;

	// Front matter.
	const meta: Record<string, string> = {};
	if (lines[0]?.trim() === '---') {
		for (n = 1; n < lines.length && lines[n].trim() !== '---'; n++) {
			const m = lines[n].match(/^\s*([\w-]+)\s*:\s*(.*?)\s*(#.*)?$/);
			if (m) meta[m[1].toLowerCase()] = m[2];
		}
		n++;
	} else warnings.push({ line: 1, message: 'thiếu phần đầu --- (mon, tac-gia...): app sẽ dùng môn đang chọn khi nhập' });
	const pick = (...keys: string[]) => keys.map((k) => meta[k]).find((v) => v !== undefined && v !== '');

	const missingImg = new Set<string>();
	const img = (src: string) => {
		if (/^data:image\//.test(src)) return src;
		const key = src.replace(/^\.\//, '');
		if (images[key]) return images[key];
		missingImg.add(key);
		return '';
	};
	// A missing image is a warning: the text still imports, the image spot says so.
	const html = (md: string) =>
		mdToHtml(md, img).replace(/<img src="" alt="([^"]*)">/g, (_m, alt) => `<i>[thiếu ảnh${alt ? ': ' + alt : ''}]</i>`);

	const course = (pick('mon', 'course') ?? '').match(/^\s*([A-Za-z]{2,}\d{3,}[A-Za-z]?)\s*(.*)$/);
	const pack: StudyPack = {
		format: PACK_FORMAT,
		id: '',
		title: pick('tieu-de', 'title') ?? '',
		version: pick('phien-ban', 'version') ?? '1.0.0',
		language: pick('ngon-ngu', 'language') ?? 'vi',
		course: {
			code: course?.[1]?.toUpperCase() ?? '',
			name: course?.[2]?.trim() || (pick('mon', 'course') ?? ''),
			...(pick('truong', 'school') ? { school: pick('truong', 'school') } : {}),
		},
		authors: (pick('tac-gia', 'authors', 'author') ?? '')
			.split(',')
			.map((s) => s.trim())
			.filter(Boolean)
			.map((name) => ({ name })),
		...(pick('giay-phep', 'license') ? { license: pick('giay-phep', 'license') } : {}),
		...(pick('tao-boi', 'created-with') ? { createdWith: pick('tao-boi', 'created-with') } : {}),
		...(pick('da-kiem', 'verified') ? { verified: { method: pick('da-kiem', 'verified')! } } : {}),
		...(pick('mo-ta', 'description') ? { description: pick('mo-ta', 'description') } : {}),
		...(pick('nguon', 'sources')
			? {
					sources: pick('nguon', 'sources')!
						.split(';')
						.map((s) => ({ title: s.trim() }))
						.filter((s) => s.title),
				}
			: {}),
		settings: {
			shuffleQuestions: yes(pick('xao-cau', 'shuffle-questions')),
			shuffleOptions: yes(pick('xao-dap-an', 'shuffle-options')),
		},
		units: [],
		exams: [],
	};

	// Body: walk headings. Lines are kept with their number for error messages.
	type Block = { level: number; title: string; line: number; body: { text: string; line: number }[] };
	const blocks: Block[] = [];
	let cur: Block | null = null;
	for (; n < lines.length; n++) {
		const h = lines[n].match(/^(#{1,4})\s+(.*)$/);
		if (h && h[1].length <= 3) {
			cur = { level: h[1].length, title: h[2].trim(), line: n + 1, body: [] };
			blocks.push(cur);
		} else if (cur) cur.body.push({ text: lines[n], line: n + 1 });
		else if (lines[n].trim()) warnings.push({ line: n + 1, message: 'chữ nằm trước chương (#) đầu tiên, bị bỏ qua' });
	}

	let unit: StudyPack['units'][number] | null = null;
	let lesson: PackLesson | null = null;
	let exam: NonNullable<StudyPack['exams']>[number] | null = null;
	let inExams = false;
	const lessonIds = new Set<string>();
	for (const b of blocks) {
		if (b.level === 1) {
			inExams = EXAM_UNIT.test(b.title);
			unit = inExams ? null : { title: b.title, lessons: [] };
			if (unit) pack.units.push(unit);
			lesson = null;
			exam = null;
		} else if (b.level === 2) {
			if (inExams) {
				exam = { id: slug(b.title, `de-${pack.exams!.length + 1}`), title: b.title, minutes: 30, questions: [] };
				const info = b.body.map((x) => x.text).join(' ');
				const min = info.match(/(\d+)\s*phút|(\d+)\s*min/i),
					right = info.match(/đúng\s*:?\s*([+-]?[\d.,/]+)/i),
					wrong = info.match(/sai\s*:?\s*([+\-−]?[\d.,/]+)/i);
				if (min) exam.minutes = +(min[1] ?? min[2]);
				if (right || wrong) exam.scoring = { count: 0, right: num(right?.[1]) ?? 1, wrong: num(wrong?.[1]) ?? 0 };
				pack.exams!.push(exam);
				lesson = null;
				continue;
			}
			if (!unit) {
				errors.push({ line: b.line, message: 'bài (##) phải nằm trong một chương (#)' });
				continue;
			}
			let id = slug(b.title, 'bai');
			while (lessonIds.has(id)) id += '-2';
			lessonIds.add(id);
			lesson = { id, title: b.title, questions: [] };
			unit.lessons.push(lesson);
			// Lesson notes: text under ## until the first ###, split by #### headings.
			const sections: { title?: string; body: string }[] = [];
			let sec: { title?: string; lines: string[] } = { lines: [] };
			for (const x of b.body) {
				const h4 = x.text.match(/^####\s+(.*)$/);
				if (h4) {
					if (sec.lines.join('').trim())
						sections.push({ ...(sec.title ? { title: sec.title } : {}), body: html(sec.lines.join('\n')) });
					sec = { title: h4[1].trim(), lines: [] };
				} else sec.lines.push(x.text);
			}
			if (sec.lines.join('').trim()) sections.push({ ...(sec.title ? { title: sec.title } : {}), body: html(sec.lines.join('\n')) });
			if (sections.length) lesson.sections = sections;
		} else {
			const owner = exam ?? lesson;
			if (!owner) {
				errors.push({ line: b.line, message: 'câu (###) phải nằm trong một bài (##) hoặc một đề' });
				continue;
			}
			const q = parseQuestion(b, html, errors);
			if (!q) continue;
			const list = (owner.questions ??= []);
			q.id = `q${list.length + 1}`;
			list.push(q);
			lineOf.set(q, b.line);
		}
	}
	for (const x of pack.exams ?? []) if (x.scoring) x.scoring.count = x.questions?.length ?? 0;
	if (!pack.exams?.length) delete pack.exams;

	pack.id =
		pick('id') ??
		slug(
			`${pick('truong', 'school') ?? 'hcmut'}-${pack.course.code}-${pack.title || pack.units[0]?.title || 'goi'}`,
			'goi-luyen-tap',
		).slice(0, 64);
	if (!pack.title) pack.title = pack.units.map((u) => u.title).join(', ') || 'Gói luyện tập';
	if (!pack.settings!.shuffleQuestions && !pack.settings!.shuffleOptions) delete pack.settings;
	for (const k of missingImg) warnings.push({ line: lineOfText(lines, k), message: `thiếu ảnh ${k} (gửi kèm trong file .zip)` });

	// Deeper checks from the shared validator, mapped back to line numbers when possible.
	if (pack.course.code) {
		const r = validatePack(pack);
		const map = (i: PackIssue): MdIssue => ({ line: lineFor(pack, i.path), message: i.message });
		// Skip what the Markdown parser already reported in plainer words (authors, empty solution).
		errors.push(...r.errors.filter((e) => !e.path.startsWith('authors') && !e.path.endsWith('.solution')).map(map));
		warnings.push(...r.warnings.filter((w) => !/^(license|verified)$/.test(w.path)).map(map));
	}
	return { pack, errors, warnings };
}

const lineOf = new WeakMap<object, number>();
function lineFor(p: StudyPack, path: string): number {
	const m = path.match(/^(units\[(\d+)\]\.lessons\[(\d+)\]|exams\[(\d+)\])\.questions\[(\d+)\]/);
	if (!m) return 0;
	const owner = m[4] !== undefined ? p.exams?.[+m[4]] : p.units[+m[2]]?.lessons[+m[3]];
	const q = owner?.questions?.[+m[5]];
	return (q && lineOf.get(q)) ?? 0;
}
const lineOfText = (lines: string[], needle: string) => lines.findIndex((l) => l.includes(needle)) + 1;
function num(s?: string): number | undefined {
	if (!s) return undefined;
	const t = s.replace('−', '-').replace(',', '.');
	const f = t.match(/^([+-]?\d+(?:\.\d+)?)\/(\d+(?:\.\d+)?)$/);
	const v = f ? +f[1] / +f[2] : Number(t);
	return Number.isFinite(v) ? v : undefined;
}

/** One ### block: prompt lines, then - [x] options or one = answer line, then > solution. */
function parseQuestion(
	b: { title: string; line: number; body: { text: string; line: number }[] },
	html: (md: string) => string,
	errors: MdIssue[],
): PackQuestion | null {
	const parts = b.title.split(/\s+·\s+/).map((s) => s.trim());
	const keepOrder = parts.some((s) => KEEP_ORDER.test(s));
	const group = parts.map((s) => GROUP.exec(s)?.[1].trim()).find((g) => !!g);
	const tag =
		parts
			.slice(1)
			.filter((s) => !KEEP_ORDER.test(s) && !GROUP.test(s))
			.join(' · ') || (/^câu\s*\d+$/i.test(parts[0]) || GROUP.test(parts[0]) ? '' : parts[0]);
	const prompt: string[] = [],
		options: { text: string; right: boolean }[] = [],
		solution: string[] = [];
	let answer: { text: string; line: number } | null = null;
	for (const x of b.body) {
		const opt = x.text.match(/^\s*[-*]\s+\[([ xX])\]\s?(.*)$/);
		if (opt) options.push({ text: opt[2], right: opt[1] !== ' ' });
		else if (/^\s*=\s?/.test(x.text) && !options.length && !solution.length)
			answer = { text: x.text.replace(/^\s*=\s?/, '').trim(), line: x.line };
		else if (/^\s*>/.test(x.text)) solution.push(x.text.replace(/^\s*>\s?/, ''));
		else if (!options.length && !answer && !solution.length) prompt.push(x.text);
		else if (x.text.trim() && !solution.length) prompt.push(x.text); // text after options: keep with prompt rather than lose it
	}
	const base = {
		...(tag ? { tag } : {}),
		prompt: html(prompt.join('\n')),
		solution: solution.length ? html(solution.join('\n')) : '',
		...(keepOrder ? { keepOrder: true } : {}),
		...(group ? { group } : {}),
	};
	if (!prompt.join('').trim()) {
		errors.push({ line: b.line, message: 'câu chưa có đề' });
		return null;
	}
	if (!solution.length) errors.push({ line: b.line, message: 'câu chưa có lời giải (dòng bắt đầu bằng >)' });
	if (options.length) {
		const right = options.map((o, i) => (o.right ? i : -1)).filter((i) => i >= 0);
		const opts = options.map((o) => html(o.text).replace(/^<p>([\s\S]*)<\/p>$/, '$1'));
		if (!right.length) {
			errors.push({ line: b.line, message: 'chưa tick đáp án đúng: đổi - [ ] thành - [x]' });
			return null;
		}
		return right.length === 1
			? { ...base, options: opts, answer: right[0] }
			: { ...base, type: 'multi', options: opts, answers: right };
	}
	if (!answer) {
		errors.push({ line: b.line, message: 'câu chưa có đáp án: thêm các dòng - [x] / - [ ], hoặc một dòng = ...' });
		return null;
	}
	const a = answer.text;
	if (/^(đúng|dung|true|t|sai|false|f)$/i.test(a)) return { ...base, type: 'truefalse', answer: /^(đúng|dung|true|t)$/i.test(a) };
	const nm = a.match(/^([+\-−]?\d[\d.,]*(?:[eE][+-]?\d+)?)\s*(?:(?:±|\+-|\+\/-)\s*([\d.,]+(?:[eE][+-]?\d+)?))?\s*(?:\(([^)]+)\))?$/);
	if (nm) {
		const v = num(nm[1]),
			tol = nm[2] ? num(nm[2]) : undefined;
		if (v === undefined) {
			errors.push({ line: answer.line, message: `không đọc được số "${nm[1]}"` });
			return null;
		}
		return { ...base, type: 'numeric', answer: v, ...(tol ? { tolerance: tol } : {}), ...(nm[3] ? { unit: nm[3].trim() } : {}) };
	}
	return {
		...base,
		type: 'short',
		accept: a
			.split('|')
			.map((s) => s.trim())
			.filter(Boolean),
	};
}

// ------------------------------------------------------------------ write

/** HTML (from the app) -> readable Markdown. Simple tags become Markdown, the rest stays as inline HTML. */
function htmlToMd(h: string, take: (dataUri: string, alt: string) => string): string {
	return h
		.replace(
			/<img\b[^>]*?src="(data:image\/[^"]+)"[^>]*?(?:alt="([^"]*)")?[^>]*>/gi,
			(_m, src, alt) => `![${alt ?? ''}](${take(src, alt ?? '')})`,
		)
		.replace(/<div class="math">\s*([\s\S]*?)\s*<\/div>/gi, '\n\n$1\n\n') // display math on its own line
		.replace(/(<div class="(?:warn|keys|tip|note)">[\s\S]*?<\/div>)/gi, '\n\n$1\n\n') // callouts stay as HTML blocks
		.replace(/<p>([\s\S]*?)<\/p>/gi, '\n\n$1\n\n')
		.replace(/<\/?(b|strong)>/gi, '**')
		.replace(/<\/?(i|em)>/gi, '*')
		.replace(/<br\s*\/?>/gi, '  \n')
		.replace(/[ \t]+\n/g, (m) => (m.length >= 3 ? '  \n' : '\n'))
		.replace(/\n{3,}/g, '\n\n')
		.trim();
}

/** Pack -> .md text + images (path -> data URI). Images are named img/1.webp, img/2.png… in order. */
export function writeMarkdown(p: StudyPack): { md: string; images: Record<string, string> } {
	const images: Record<string, string> = {};
	const seen = new Map<string, string>();
	const take = (src: string) => {
		const hit = seen.get(src);
		if (hit) return hit;
		const ext = src.match(/^data:image\/(png|jpeg|gif|webp)/)?.[1]?.replace('jpeg', 'jpg') ?? 'png';
		const path = `img/${seen.size + 1}.${ext}`;
		seen.set(src, path);
		images[path] = src;
		return path;
	};
	const md = (h: string) => htmlToMd(h, take);
	const one = (h: string) => md(h).replace(/\s*\n\s*/g, ' '); // options must stay on one line
	const out: string[] = [
		'---',
		`id: ${p.id}`,
		`tieu-de: ${p.title}`,
		`mon: ${p.course.code} ${p.course.name}`,
		...(p.course.school ? [`truong: ${p.course.school}`] : []),
		`tac-gia: ${p.authors.map((a) => a.name).join(', ')}`,
		`phien-ban: ${p.version}`,
		...(p.license ? [`giay-phep: ${p.license}`] : []),
		...(p.createdWith ? [`tao-boi: ${p.createdWith}`] : []),
		...(p.verified ? [`da-kiem: ${p.verified.method}`] : []),
		...(p.settings?.shuffleQuestions ? ['xao-cau: co'] : []),
		...(p.settings?.shuffleOptions ? ['xao-dap-an: co'] : []),
		'---',
		'',
	];
	const question = (q: PackQuestion, i: number) => {
		const head = [
			`Câu ${i + 1}`,
			...(q.tag ? [q.tag] : []),
			...(q.keepOrder ? ['giữ thứ tự'] : []),
			...(q.group ? [`nhóm: ${q.group}`] : []),
		].join(' · ');
		out.push(`### ${head}`, md(q.prompt));
		const t = q.type ?? 'single';
		if (t === 'single' || t === 'multi') {
			const right = t === 'multi' ? (q.answers ?? []) : [q.answer as number];
			(q.options ?? []).forEach((o, j) => out.push(`- [${right.includes(j) ? 'x' : ' '}] ${one(o)}`));
		} else if (t === 'truefalse') out.push(`= ${q.answer ? 'Đúng' : 'Sai'}`);
		else if (t === 'numeric') out.push(`= ${q.answer}${q.tolerance ? ` ± ${q.tolerance}` : ''}${q.unit ? ` (${q.unit})` : ''}`);
		else out.push(`= ${(q.accept ?? []).join(' | ')}`);
		out.push(
			...md(q.solution)
				.split('\n')
				.map((l) => (l ? `> ${l}` : '>')),
			'',
		);
	};
	for (const u of p.units) {
		out.push(`# ${u.title}`, '');
		for (const l of u.lessons) {
			out.push(`## ${l.title}`, '');
			for (const s of l.sections ?? []) out.push(...(s.title ? [`#### ${s.title}`] : []), md(s.body), '');
			(l.questions ?? []).forEach(question);
		}
	}
	if (p.exams?.length) {
		// Markdown exams are self-contained: questions referenced from lessons are copied in.
		const byRef = new Map<string, PackQuestion>(
			p.units.flatMap((u) =>
				u.lessons.flatMap((l) =>
					(l.questions ?? []).map((q, i): [string, PackQuestion] => [`${l.id}.${q.id ?? 'q' + (i + 1)}`, q]),
				),
			),
		);
		out.push('# Đề thi thử', '');
		for (const ex of p.exams) {
			const x = {
				...ex,
				questions: [
					...(ex.questionRefs ?? []).map((r) => byRef.get(r)).filter((q): q is PackQuestion => !!q),
					...(ex.questions ?? []),
				],
			};
			const sc = x.scoring ? ` · Đúng: ${+x.scoring.right.toFixed(4)} · Sai: ${+x.scoring.wrong.toFixed(4)}` : '';
			out.push(`## ${x.title}`, `Thời gian: ${x.minutes} phút${sc}`, '');
			(x.questions ?? []).forEach(question);
		}
	}
	return { md: out.join('\n').replace(/\n{3,}/g, '\n\n'), images };
}
