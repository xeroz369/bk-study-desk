// Đổi qua lại giữa gói luyện tập và các định dạng quiz của Moodle (BK-LMS chạy Moodle):
//   Aiken      chỉ trắc nghiệm một đáp án, dễ soạn nhất (gõ trong Word/Notepad).
//   GIFT       dạng chữ, đủ loại: một/nhiều đáp án, đúng/sai, điền số, điền chữ.
//   Moodle XML giảng viên xuất từ ngân hàng câu hỏi LMS; có ảnh (@@PLUGINFILE@@ + <file base64>) và category (→ bài).
// Loại chưa hỗ trợ (ghép cặp, tự luận, cloze, kéo thả) được bỏ qua và ghi vào notes để người dùng biết.

import { plainText } from './text';
import type { PackLesson, PackQuestion, StudyPack } from './pack';

export type QuizFormat = 'studypack' | 'markdown' | 'moodlexml' | 'gift' | 'aiken';

export interface Converted {
	lessons: PackLesson[];
	notes: string[];
}

export function detectFormat(raw: string): QuizFormat | null {
	const t = raw.trim().replace(/^```\w*\s*|\s*```$/g, '');
	if (t.startsWith('{') && /"format"\s*:\s*"studypack\//.test(t)) return 'studypack';
	if (/<quiz[\s>]/i.test(t)) return 'moodlexml';
	// Study Markdown before GIFT: TeX braces (\frac{a}{b}) would look like GIFT answer blocks.
	if (/^---\s*$/m.test(t.split('\n')[0] ?? '') || (/^###\s+/m.test(t) && /^\s*([-*]\s+\[[ xX]\]|=\s|>)/m.test(t))) return 'markdown';
	if (/^\s*ANSWER\s*:\s*[A-Z]\s*$/im.test(t) && !/\{[^}]*[=~#]/.test(t)) return 'aiken';
	if (/\{[^{}]*\}/.test(t) && /[=~]|\{\s*(T|F|TRUE|FALSE)\s*\}|\{\s*#/.test(t)) return 'gift';
	if (t.startsWith('{')) return 'studypack';
	return null;
}

const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
/** Chữ thường (Aiken/GIFT) → HTML đoạn văn; giữ TeX nguyên. */
const para = (s: string) =>
	s
		.trim()
		.split(/\n{2,}/)
		.map((p) => `<p>${esc(p).replace(/\n/g, '<br>')}</p>`)
		.join('');

// ------------------------------------------------------------------ Aiken

export function parseAiken(raw: string): Converted {
	const notes: string[] = [];
	const questions: PackQuestion[] = [];
	const lines = raw.replace(/\r/g, '').split('\n');
	let stem: string[] = [],
		opts: string[] = [];
	const flush = (answerLetter: string) => {
		const answer = answerLetter.charCodeAt(0) - 65;
		if (stem.length && opts.length >= 2 && answer >= 0 && answer < opts.length)
			questions.push({
				prompt: para(stem.join('\n')),
				options: opts.map((o) => esc(o)),
				answer,
				solution: '<p>Đáp án đúng: ' + answerLetter + '.</p>',
			});
		else notes.push(`Bỏ một câu Aiken không đủ đề/phương án: "${stem.join(' ').slice(0, 40)}..."`);
		stem = [];
		opts = [];
	};
	for (const line of lines) {
		const opt = line.match(/^\s*([A-H])[.)]\s+(.*)$/);
		const ans = line.match(/^\s*ANSWER\s*:\s*([A-H])\s*$/i);
		if (ans) flush(ans[1].toUpperCase());
		else if (opt && (opts.length || stem.length)) opts.push(opt[2]);
		else if (line.trim() && !opts.length) stem.push(line);
	}
	if (questions.length) notes.push('Aiken không có lời giải: nên bổ sung lời giải để tự học được.');
	return { lessons: [{ id: 'aiken', title: 'Câu nhập từ Aiken', questions }], notes };
}

// ------------------------------------------------------------------ GIFT

const unescapeGift = (s: string) => s.replace(/\\([~=#{}:\\])/g, '$1').replace(/\\n/g, '\n');
/** Tách theo ký tự đặc biệt chưa escape (\~ \= …). */
function splitUnescaped(s: string, re: RegExp): string[] {
	const out: string[] = [];
	let cur = '';
	for (let i = 0; i < s.length; i++) {
		if (s[i] === '\\' && i + 1 < s.length) {
			cur += s[i] + s[i + 1];
			i++;
			continue;
		}
		if (re.test(s[i])) {
			out.push(cur);
			cur = s[i];
			continue;
		}
		cur += s[i];
	}
	out.push(cur);
	return out;
}

export function parseGift(raw: string): Converted {
	const notes: string[] = [];
	const byCategory = new Map<string, PackQuestion[]>();
	let category = 'Câu nhập từ GIFT';
	const blocks = raw.replace(/\r/g, '').split(/\n\s*\n/);
	for (let block of blocks) {
		block = block
			.split('\n')
			.filter((l) => !/^\s*\/\//.test(l))
			.join('\n')
			.trim();
		if (!block) continue;
		// $CATEGORY may sit right above the first question (no blank line): take it, then parse the rest.
		const cat = block.match(/^\$CATEGORY:\s*(.+)$/m);
		if (cat) {
			category =
				cat[1]
					.split('/')
					.filter((x) => x && x !== '$course$' && x !== 'top')
					.at(-1) ?? category;
			block = block.replace(cat[0], '').trim();
			if (!block) continue;
		}
		let body = block,
			title = '';
		const t = body.match(/^::((?:\\:|[^:])*)::/);
		if (t) {
			title = unescapeGift(t[1]);
			body = body.slice(t[0].length);
		}
		const isHtml = /^\s*\[html\]/i.test(body);
		body = body.replace(/^\s*\[(html|moodle|plain|markdown)\]/i, '');
		// Khối đáp án {...} đầu tiên chưa escape.
		let open = -1,
			close = -1;
		for (let i = 0; i < body.length; i++) {
			if (body[i] === '\\') {
				i++;
				continue;
			}
			if (body[i] === '{' && open < 0) open = i;
			else if (body[i] === '}' && open >= 0) {
				close = i;
				break;
			}
		}
		if (open < 0 || close < 0) {
			notes.push(`Bỏ câu GIFT không có {...}: "${(title || body).slice(0, 40)}..."`);
			continue;
		}
		const before = body.slice(0, open).trim(),
			after = body.slice(close + 1).trim();
		const stemRaw = unescapeGift(after ? `${before} ____ ${after}` : before);
		const prompt = isHtml ? stemRaw : para(stemRaw);
		let inner = body.slice(open + 1, close).trim();
		let general = '';
		const g = inner.match(/(?<!\\)####([\s\S]*)$/); // lời giải chung của câu
		if (g) {
			general = unescapeGift(g[1]).trim();
			inner = inner.slice(0, g.index).trim();
		}
		const solution = general ? (isHtml ? general : para(general)) : '<p>(Chưa có lời giải trong file GIFT.)</p>';
		const push = (q: PackQuestion) => {
			if (!byCategory.has(category)) byCategory.set(category, []);
			byCategory.get(category)!.push({ ...(title ? { tag: title } : {}), ...q });
		};
		if (!inner) {
			notes.push(`Bỏ câu tự luận (essay): "${(title || before).slice(0, 40)}..."`);
			continue;
		}
		const tf = inner.match(/^(T|TRUE|F|FALSE)\b/i);
		if (tf) {
			push({ type: 'truefalse', prompt, answer: /^T/i.test(tf[1]), solution });
			continue;
		}
		if (inner.startsWith('#')) {
			// {#3.14:0.01}, {#1..2}, {#=3.14:0.01 =%50%3.1:0.1}: lấy đáp án đầu (đủ điểm), bỏ feedback và trọng số.
			const first = (
				splitUnescaped(inner.slice(1), /=/)
					.map((x) => x.replace(/^=/, '').trim())
					.filter(Boolean)[0] ?? ''
			)
				.replace(/(?<!\\)#.*$/, '')
				.replace(/^%-?[\d.]+%/, '')
				.trim();
			const range = first.match(/^(-?[\d.]+)\.\.(-?[\d.]+)$/);
			const tol = first.match(/^(-?[\d.eE+-]+):([\d.eE+-]+)$/);
			if (range)
				push({
					type: 'numeric',
					prompt,
					answer: (+range[1] + +range[2]) / 2,
					tolerance: Math.abs(+range[2] - +range[1]) / 2,
					solution,
				});
			else if (tol) push({ type: 'numeric', prompt, answer: +tol[1], tolerance: +tol[2], solution });
			else if (Number.isFinite(+first)) push({ type: 'numeric', prompt, answer: +first, solution });
			else notes.push(`Bỏ câu số không đọc được: "${inner.slice(0, 30)}"`);
			continue;
		}
		if (/->/.test(inner)) {
			notes.push(`Bỏ câu ghép cặp (matching), chưa hỗ trợ: "${(title || before).slice(0, 40)}..."`);
			continue;
		}
		const answers = splitUnescaped(inner, /[=~]/)
			.map((x) => x.trim())
			.filter(Boolean)
			.map((a) => {
				const right = a.startsWith('=');
				let text = a.slice(1).trim();
				const w = text.match(/^%(-?[\d.]+)%/);
				const weight = w ? +w[1] : right ? 100 : 0;
				if (w) text = text.slice(w[0].length);
				const fb = text.search(/(^|[^\\])#/);
				const feedback = fb >= 0 ? unescapeGift(text.slice(fb + (text[fb] === '#' ? 1 : 2))).trim() : '';
				if (fb >= 0) text = text.slice(0, fb + (text[fb] === '#' ? 0 : 1));
				return { text: unescapeGift(text.trim()), right, weight, feedback };
			});
		const wrong = answers.filter((a) => !a.right && a.weight <= 0);
		if (!wrong.length && answers.every((a) => a.right)) {
			push({ type: 'short', prompt, accept: answers.map((a) => a.text), solution });
			continue;
		}
		const fbList = answers
			.filter((a) => a.feedback)
			.map((a) => `<li>${esc(a.text)}: ${esc(a.feedback)}</li>`)
			.join('');
		const sol = fbList ? `${solution}<ul>${fbList}</ul>` : solution;
		const opts = answers.map((a) => (isHtml ? a.text : esc(a.text)));
		const correct = answers.map((a, i) => (a.weight > 0 ? i : -1)).filter((i) => i >= 0);
		if (correct.length === 1 && answers.filter((a) => a.right).length <= 1)
			push({ prompt, options: opts, answer: correct[0], solution: sol });
		else if (correct.length > 1) push({ type: 'multi', prompt, options: opts, answers: correct, solution: sol });
		else notes.push(`Bỏ câu không có đáp án đúng: "${(title || before).slice(0, 40)}..."`);
	}
	const lessons = [...byCategory].map(([title, questions], i) => ({ id: `gift-${i + 1}`, title, questions }));
	return { lessons, notes };
}

// ------------------------------------------------------------------ Moodle XML

/** Ảnh trong Moodle XML: <file name="a.png" encoding="base64">…</file> + src="@@PLUGINFILE@@/a.png" → data URI. */
function withFiles(el: Element | null): string {
	if (!el) return '';
	let html = el.querySelector(':scope > text')?.textContent ?? '';
	for (const f of el.querySelectorAll(':scope > file')) {
		const name = f.getAttribute('name') ?? '';
		const ext = name.split('.').pop()?.toLowerCase();
		const mime =
			ext === 'jpg' || ext === 'jpeg'
				? 'image/jpeg'
				: ext === 'gif'
					? 'image/gif'
					: ext === 'webp'
						? 'image/webp'
						: ext === 'png'
							? 'image/png'
							: '';
		if (!mime) continue;
		const data = `data:${mime};base64,${(f.textContent ?? '').replace(/\s+/g, '')}`;
		html = html
			.split(`@@PLUGINFILE@@/${encodeURIComponent(name)}`)
			.join(data)
			.split(`@@PLUGINFILE@@/${name}`)
			.join(data);
	}
	return el.getAttribute('format') === 'html' || /<[a-z]/i.test(html) ? html : para(html);
}

export function parseMoodleXml(raw: string): Converted {
	const notes: string[] = [];
	const doc = new DOMParser().parseFromString(raw, 'application/xml');
	if (doc.querySelector('parsererror')) return { lessons: [], notes: ['File XML lỗi, không đọc được.'] };
	const byCategory = new Map<string, PackQuestion[]>();
	let category = 'Câu nhập từ Moodle';
	const skipped: Record<string, number> = {};
	for (const q of doc.querySelectorAll('quiz > question')) {
		const type = q.getAttribute('type') ?? '';
		if (type === 'category') {
			const path = q.querySelector('category > text')?.textContent ?? '';
			category =
				path
					.split('/')
					.filter((x) => x && x !== '$course$' && x !== '$system$' && x !== 'top' && !x.startsWith('Default for'))
					.at(-1) ?? category;
			continue;
		}
		const name = q.querySelector(':scope > name > text')?.textContent?.trim() ?? '';
		const prompt = withFiles(q.querySelector(':scope > questiontext'));
		const general = withFiles(q.querySelector(':scope > generalfeedback'));
		const answers = [...q.querySelectorAll(':scope > answer')].map((a) => ({
			html: withFiles(a),
			fraction: parseFloat(a.getAttribute('fraction') ?? '0'),
			feedback: withFiles(a.querySelector(':scope > feedback')),
			tol: parseFloat(a.querySelector(':scope > tolerance')?.textContent ?? '0'),
		}));
		const fb = answers
			.filter((a) => plainText(a.feedback).trim())
			.map((a) => `<li>${a.html.replace(/<\/?p>/g, '')}: ${a.feedback.replace(/<\/?p>/g, '')}</li>`)
			.join('');
		const solution =
			(plainText(general).trim() ? general : '<p>(Chưa có lời giải trong ngân hàng câu hỏi.)</p>') + (fb ? `<ul>${fb}</ul>` : '');
		const base = { ...(name ? { tag: name } : {}), prompt, solution };
		let item: PackQuestion | null = null;
		if (type === 'multichoice') {
			const single = (q.querySelector(':scope > single')?.textContent ?? 'true').trim() !== 'false';
			const options = answers.map((a) => a.html.replace(/^<p>(.*)<\/p>$/s, '$1'));
			const right = answers.map((a, i) => (a.fraction > 0 ? i : -1)).filter((i) => i >= 0);
			if (single && right.length)
				item = { ...base, options, answer: answers.reduce((b, a, i, arr) => (a.fraction > arr[b].fraction ? i : b), 0) };
			else if (right.length) item = { ...base, type: 'multi', options, answers: right };
		} else if (type === 'truefalse') {
			const t = answers.find((a) => a.fraction > 0);
			if (t) item = { ...base, type: 'truefalse', answer: /true|đúng/i.test(t.html) };
		} else if (type === 'numerical') {
			const a = answers.filter((x) => x.fraction >= 100)[0] ?? answers[0];
			const v = a ? parseFloat(plainText(a.html)) : NaN;
			if (Number.isFinite(v)) item = { ...base, type: 'numeric', answer: v, ...(a.tol ? { tolerance: a.tol } : {}) };
		} else if (type === 'shortanswer') {
			const acc = answers
				.filter((a) => a.fraction >= 100)
				.map((a) => plainText(a.html).trim())
				.filter(Boolean);
			if (acc.length) item = { ...base, type: 'short', accept: acc };
		} else {
			skipped[type] = (skipped[type] ?? 0) + 1;
			continue;
		}
		if (!item) {
			notes.push(`Bỏ câu "${name || type}": không có đáp án đúng.`);
			continue;
		}
		if (!byCategory.has(category)) byCategory.set(category, []);
		byCategory.get(category)!.push(item);
	}
	const names: Record<string, string> = {
		matching: 'ghép cặp',
		essay: 'tự luận',
		multianswer: 'cloze',
		description: 'mô tả',
		ddwtos: 'kéo thả',
		gapselect: 'chọn chỗ trống',
		randomsamatch: 'ghép ngẫu nhiên',
		calculated: 'tính toán có biến',
	};
	for (const [t, n] of Object.entries(skipped)) notes.push(`Bỏ ${n} câu loại ${names[t] ?? t} (chưa hỗ trợ).`);
	return { lessons: [...byCategory].map(([title, questions], i) => ({ id: `moodle-${i + 1}`, title, questions })), notes };
}

// ------------------------------------------------------------------ xuất ra Moodle

const cdata = (s: string) => `<![CDATA[${s.replace(/]]>/g, ']]]]><![CDATA[>')}]]>`;
/** Ảnh data URI → <file> + @@PLUGINFILE@@ (Moodle không nhận data URI trong ngân hàng câu hỏi). */
function moodleText(tag: string, html: string, n: { i: number }) {
	const files: string[] = [];
	const out = html.replace(/src="data:image\/(png|jpeg|gif|webp);base64,([^"]+)"/g, (_m, ext: string, b64: string) => {
		const name = `img${++n.i}.${ext === 'jpeg' ? 'jpg' : ext}`;
		files.push(`<file name="${name}" path="/" encoding="base64">${b64.replace(/\s+/g, '')}</file>`);
		return `src="@@PLUGINFILE@@/${name}"`;
	});
	return `<${tag} format="html"><text>${cdata(out)}</text>${files.join('')}</${tag}>`;
}

export function toMoodleXml(p: StudyPack): string {
	const n = { i: 0 };
	const out: string[] = ['<?xml version="1.0" encoding="UTF-8"?>', '<quiz>'];
	for (const u of p.units)
		for (const l of u.lessons) {
			out.push(
				`<question type="category"><category><text>$course$/top/${esc(p.course.name)}/${esc(u.title)}/${esc(l.title)}</text></category></question>`,
			);
			for (const [i, q] of (l.questions ?? []).entries()) {
				const head = `<name><text>${esc(q.tag ?? `${l.title} · câu ${i + 1}`)}</text></name>${moodleText('questiontext', q.prompt, n)}${moodleText('generalfeedback', q.solution, n)}<defaultgrade>1</defaultgrade><penalty>0.3333333</penalty><hidden>0</hidden>`;
				const t = q.type ?? 'single';
				if (t === 'single' || t === 'multi') {
					const right = t === 'multi' ? (q.answers ?? []) : [q.answer as number];
					const wrongN = (q.options?.length ?? 0) - right.length;
					// Nhiều đáp án: đúng chia đều 100%, sai trừ đều 100% (chọn bừa hết thì 0 điểm), như cách Moodle chấm.
					const ans = (q.options ?? [])
						.map((o, j) => {
							const f = right.includes(j)
								? t === 'multi'
									? +(100 / right.length).toFixed(5)
									: 100
								: t === 'multi' && wrongN
									? -+(100 / wrongN).toFixed(5)
									: 0;
							const body = moodleText('x', o, n).replace(/^<x format="html">|<\/x>$/g, ''); // <text> + <file> của ảnh trong phương án
							return `<answer fraction="${f}" format="html">${body}<feedback format="html"><text></text></feedback></answer>`;
						})
						.join('');
					out.push(
						`<question type="multichoice">${head}<single>${t === 'single'}</single><shuffleanswers>true</shuffleanswers><answernumbering>ABCD</answernumbering>${ans}</question>`,
					);
				} else if (t === 'truefalse') {
					const yes = q.answer === true;
					out.push(
						`<question type="truefalse">${head}<answer fraction="${yes ? 100 : 0}" format="moodle_auto_format"><text>true</text></answer><answer fraction="${yes ? 0 : 100}" format="moodle_auto_format"><text>false</text></answer></question>`,
					);
				} else if (t === 'numeric') {
					out.push(
						`<question type="numerical">${head}<answer fraction="100" format="moodle_auto_format"><text>${q.answer}</text><tolerance>${q.tolerance ?? 0}</tolerance></answer></question>`,
					);
				} else if (t === 'short') {
					out.push(
						`<question type="shortanswer">${head}<usecase>0</usecase>${(q.accept ?? []).map((a) => `<answer fraction="100" format="moodle_auto_format"><text>${esc(a)}</text></answer>`).join('')}</question>`,
					);
				}
			}
		}
	out.push('</quiz>');
	return out.join('\n');
}

const giftEsc = (s: string) => s.replace(/([~=#{}:\\])/g, '\\$1').replace(/\n/g, ' ');
export function toGift(p: StudyPack): string {
	const out: string[] = [`// ${p.title} · Study Pack ${p.id} v${p.version}`];
	for (const u of p.units)
		for (const l of u.lessons) {
			out.push('', `$CATEGORY: $course$/top/${p.course.name}/${u.title}/${l.title}`, '');
			for (const [i, q] of (l.questions ?? []).entries()) {
				const title = `::${giftEsc(q.tag ?? `${l.title} câu ${i + 1}`)}::[html]${giftEsc(q.prompt)}`;
				const fb = `####${giftEsc(q.solution)}`;
				const t = q.type ?? 'single';
				let body = '';
				if (t === 'single') body = (q.options ?? []).map((o, j) => `${j === q.answer ? '=' : '~'}${giftEsc(o)}`).join(' ');
				else if (t === 'multi') {
					const right = q.answers ?? [],
						wrongN = (q.options?.length ?? 0) - right.length;
					body = (q.options ?? [])
						.map(
							(o, j) =>
								`~%${right.includes(j) ? +(100 / right.length).toFixed(5) : wrongN ? -+(100 / wrongN).toFixed(5) : 0}%${giftEsc(o)}`,
						)
						.join(' ');
				} else if (t === 'truefalse') body = q.answer ? 'T' : 'F';
				else if (t === 'numeric') body = `#${q.answer}${q.tolerance ? ':' + q.tolerance : ''}`;
				else if (t === 'short') body = (q.accept ?? []).map((a) => '=' + giftEsc(a)).join(' ');
				out.push(`${title} {${body} ${fb}}`, '');
			}
		}
	return out.join('\n');
}
