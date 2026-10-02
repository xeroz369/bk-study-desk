// Xuất bài đã soạn trong content/*.js thành gói luyện tập để chia sẻ (không đổi content/ đang dùng).
//   node studypack/export.mjs <môn> [--lessons id1,id2] [--id ten-goi] [--author "Tên"] [--out file]
// Ví dụ: node studypack/export.mjs ppt --lessons ppt-02 --id hcmut-mt1009-chia-doi --author "xeroz369"
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const opt = (name, def) => {
	const i = args.indexOf('--' + name);
	return i >= 0 ? args[i + 1] : def;
};
const courseId = args[0];
if (!courseId || courseId.startsWith('--')) {
	console.log('Cách dùng: node studypack/export.mjs <môn> [--lessons id1,id2] [--id ten-goi] [--author "Tên"] [--out file]');
	process.exit(2);
}

// content/*.js gọi Study.setManifest/addLesson/addExam: chạy trong sandbox với Study giả để lấy dữ liệu.
const store = { manifest: null, lessons: {}, exams: {} };
const Study = {
	setManifest: (m) => (store.manifest = m),
	addPage: () => {},
	addLesson: (l) => (store.lessons[l.id] = l),
	addExam: (x) => (store.exams[x.id] = x),
};
const run = (rel) => vm.runInNewContext(readFileSync(join(root, rel), 'utf8'), { Study, String }, { filename: rel });
run('content/manifest.js');
const course = store.manifest.courses.find((c) => c.id === courseId);
if (!course) throw new Error(`Không có môn "${courseId}" trong content/manifest.js`);
const pick = opt('lessons')?.split(',');
for (const u of course.units) for (const e of u.lessons) if (e.file && (!pick || pick.includes(e.id))) run(e.file);
for (const x of store.manifest.exams ?? []) run(x.file);

const slug = (s) => s.toLowerCase().replace(/[^a-z0-9-]+/g, '-').replace(/^-+|-+$/g, '');
const qid = (id) => slug(id.split('.').at(-1));
const src = (s) => ({ title: s.file, ...(s.pages ? { pages: s.pages } : {}) });
const question = (q) => ({ id: qid(q.id ?? ''), ...(q.tag ? { tag: q.tag } : {}), prompt: q.prompt, options: q.options, answer: q.answer, solution: q.solution });

const units = course.units
	.map((u) => ({
		title: u.title,
		lessons: u.lessons
			.filter((e) => store.lessons[e.id] && (!pick || pick.includes(e.id)))
			.map((e) => {
				const l = store.lessons[e.id];
				return {
					id: slug(l.id),
					title: l.title,
					...(l.sources?.length ? { sources: l.sources.map(src) } : {}),
					...(l.sections?.length ? { sections: l.sections.map((s) => ({ ...(s.title ? { title: s.title } : {}), body: s.html.trim() })) } : {}),
					questions: (l.questions ?? []).map((q, i) => question({ ...q, id: q.id ?? `q${i + 1}` })),
				};
			}),
	}))
	.filter((u) => u.lessons.length);
const included = new Set(units.flatMap((u) => u.lessons.map((l) => l.id)));
const exams = Object.values(store.exams)
	.filter((x) => x.courseId === courseId)
	.map((x) => {
		const refs = (x.questionIds ?? []).map((id) => { const [l, q] = id.split('.'); return `${slug(l)}.${slug(q)}`; });
		if (refs.some((r) => !included.has(r.split('.')[0]))) return null;   // đề tham chiếu bài không xuất thì bỏ
		if (pick && (!refs.length || x.questions?.length)) return null;         // chỉ xuất vài bài: chỉ lấy đề dựng từ chính các bài đó
		return {
			id: slug(x.id), title: x.title, minutes: x.minutes, ...(x.scoring ? { scoring: x.scoring } : {}),
			...(refs.length ? { questionRefs: refs } : {}),
			...(x.questions?.length ? { questions: x.questions.map((q, i) => question({ ...q, id: q.id ?? `q${i + 1}` })) } : {}),
		};
	})
	.filter(Boolean);

const pack = {
	format: 'studypack/1',
	id: opt('id', slug(`${course.code ?? course.id}-${pick ? pick.join('-') : 'tron-bo'}`)),
	title: opt('title', pick ? `${course.name}: ${units.flatMap((u) => u.lessons.map((l) => l.title)).join(', ')}` : course.name),
	version: opt('version', '1.0.0'),
	language: 'vi',
	course: { code: course.code ?? course.id.toUpperCase(), name: course.name, school: 'HCMUT' },
	authors: [{ name: opt('author', 'Study Pack') }],
	license: 'CC-BY-SA-4.0',
	createdWith: 'studypack/export.mjs (từ content/*.js)',
	verified: { method: 'đáp án đã tính lại bằng Python hoặc máy tính khi soạn' },
	units,
	...(exams.length ? { exams } : {}),
};
const out = opt('out', join(root, 'studypack', 'examples', `${pack.id}.studypack.json`));
writeFileSync(out, JSON.stringify(pack, null, 2) + '\n', 'utf8');
console.log(`Đã xuất ${out}: ${units.reduce((n, u) => n + u.lessons.length, 0)} bài, ${exams.length} đề`);
