// Check packs before sharing: node studypack/validate.ts <file.md | file.zip | file.studypack.json> [...]
// Same validator as the app (src/ui/src/lib/study), so a file that passes here imports in the app.
// Exit code 1 on errors: an AI agent can run this, read the errors and fix the file itself.
import { readFileSync, statSync } from 'node:fs';
import { strFromU8, unzipSync } from '../src/ui/node_modules/fflate/esm/index.mjs';
import { parseMarkdown } from '../src/ui/src/lib/study/markdown.ts';
import { validatePack } from '../src/ui/src/lib/study/pack.ts';

const files = process.argv.slice(2);
if (!files.length) {
	console.log('Cách dùng: node studypack/validate.ts <file.md | file.zip | file.studypack.json> [...]');
	process.exit(2);
}
const MIME: Record<string, string> = { png: 'image/png', jpg: 'image/jpeg', jpeg: 'image/jpeg', gif: 'image/gif', webp: 'image/webp' };
let failed = false;
for (const f of files) {
	let errors: string[] = [], warnings: string[] = [], stats = '';
	try {
		if (/\.(md|zip)$/i.test(f)) {
			let md = '';
			const images: Record<string, string> = {};
			if (f.toLowerCase().endsWith('.zip'))
				for (const [p, data] of Object.entries(unzipSync(readFileSync(f)))) {
					const ext = p.split('.').pop()!.toLowerCase();
					if (p.endsWith('.md')) md = strFromU8(data);
					else if (MIME[ext]) images[p.replace(/^[^/]+\/(img\/)/, '$1')] = `data:${MIME[ext]};base64,${Buffer.from(data).toString('base64')}`;
				}
			else md = readFileSync(f, 'utf8');
			const r = parseMarkdown(md, images);
			errors = r.errors.map((e) => `dòng ${e.line || '?'}: ${e.message}`);
			warnings = r.warnings.map((w) => `dòng ${w.line || '?'}: ${w.message}`);
			const qs = r.pack.units.reduce((n, u) => n + u.lessons.reduce((m, l) => m + (l.questions?.length ?? 0), 0), 0);
			stats = `${r.pack.units.length} chương, ${qs} câu, ${Object.keys(images).length} ảnh`;
		} else {
			const r = validatePack(JSON.parse(readFileSync(f, 'utf8')), statSync(f).size);
			errors = r.errors.map((e) => `${e.path || '(gói)'}: ${e.message}`);
			warnings = r.warnings.map((w) => `${w.path || '(gói)'}: ${w.message}`);
			stats = `${r.stats.lessons} bài, ${r.stats.questions} câu, ${r.stats.exams} đề`;
		}
	} catch (e) {
		errors = [`không đọc được file: ${e instanceof Error ? e.message : e}`];
	}
	console.log(`${errors.length ? '✗' : '✓'} ${f}${stats ? `  (${stats})` : ''}`);
	for (const e of errors) console.log(`  LỖI      ${e}`);
	for (const w of warnings) console.log(`  cảnh báo ${w}`);
	if (errors.length) failed = true;
}
process.exit(failed ? 1 : 0);
