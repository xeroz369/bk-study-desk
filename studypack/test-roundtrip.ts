// Round-trip test on every example: (.md/.zip ->) pack -> Markdown -> pack must keep every question.
// Run: node studypack/test-roundtrip.ts
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { strFromU8, unzipSync } from '../src/ui/node_modules/fflate/esm/index.mjs';
import { parseMarkdown, writeMarkdown } from '../src/ui/src/lib/study/markdown.ts';
import type { PackQuestion, StudyPack } from '../src/ui/src/lib/study/pack.ts';

const dir = fileURLToPath(new URL('./examples/', import.meta.url));
const MIME: Record<string, string> = { png: 'image/png', jpg: 'image/jpeg', gif: 'image/gif', webp: 'image/webp' };
const key = (q: PackQuestion) => JSON.stringify([q.type ?? 'single', q.answer, q.answers, q.accept, q.tolerance, q.options?.length]);
const all = (p: StudyPack) => {
	const lessonQs = p.units.flatMap((u) => u.lessons.flatMap((l) => (l.questions ?? []).map((q, i) => [`${l.id}.${q.id ?? 'q' + (i + 1)}`, q] as const)));
	const byRef = new Map(lessonQs);
	// Exams may reference lesson questions (JSON) or carry copies (Markdown): count both the same way.
	return [...lessonQs.map(([, q]) => q), ...(p.exams ?? []).flatMap((x) => [...(x.questionRefs ?? []).map((r) => byRef.get(r)!).filter(Boolean), ...(x.questions ?? [])])];
};

function load(file: string): StudyPack {
	if (file.endsWith('.json')) return JSON.parse(readFileSync(file, 'utf8'));
	let md = '';
	const images: Record<string, string> = {};
	if (file.endsWith('.zip'))
		for (const [p, d] of Object.entries(unzipSync(readFileSync(file)))) {
			const ext = p.split('.').pop()!.toLowerCase();
			if (p.endsWith('.md')) md = strFromU8(d);
			else if (MIME[ext]) images[p] = `data:${MIME[ext]};base64,${Buffer.from(d).toString('base64')}`;
		}
	else md = readFileSync(file, 'utf8');
	return parseMarkdown(md, images).pack;
}

let failed = 0;
for (const name of readdirSync(dir).filter((f) => /\.(md|zip|json)$/.test(f))) {
	const src = load(join(dir, name));
	const { md, images } = writeMarkdown(src);
	const back = parseMarkdown(md, images);
	const a = all(src), b = all(back.pack);
	const diff = a.filter((q, i) => key(q) !== key(b[i] ?? ({} as PackQuestion))).length;
	const ok = !back.errors.length && a.length === b.length && diff === 0;
	if (!ok) failed++;
	console.log(`${ok ? '✓' : '✗'} ${name}: ${a.length} → ${b.length} câu, ${Object.keys(images).length} ảnh, lệch ${diff}`);
	for (const e of back.errors.slice(0, 5)) console.log(`   dòng ${e.line}: ${e.message}`);
}
process.exit(failed ? 1 : 0);
