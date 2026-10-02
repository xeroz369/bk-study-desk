// Convert between pack JSON and Study Markdown (.md, or .zip when there are images).
//   node studypack/convert.ts <in.studypack.json> [out.md|out.zip]
//   node studypack/convert.ts <in.md|in.zip> [out.studypack.json]
import { readFileSync, writeFileSync } from 'node:fs';
import { strToU8, unzipSync, zipSync, strFromU8 } from '../src/ui/node_modules/fflate/esm/index.mjs';
import { parseMarkdown, writeMarkdown } from '../src/ui/src/lib/study/markdown.ts';
import type { StudyPack } from '../src/ui/src/lib/study/pack.ts';

const [input, output] = process.argv.slice(2);
if (!input) {
	console.log('Cách dùng: node studypack/convert.ts <file.studypack.json | file.md | file.zip> [đầu ra]');
	process.exit(2);
}
const b64 = (u: Uint8Array) => Buffer.from(u).toString('base64');
const mime = (p: string) => ({ png: 'image/png', jpg: 'image/jpeg', jpeg: 'image/jpeg', gif: 'image/gif', webp: 'image/webp' })[p.split('.').pop()!.toLowerCase()];

if (input.endsWith('.json')) {
	const { md, images } = writeMarkdown(JSON.parse(readFileSync(input, 'utf8')) as StudyPack);
	const hasImg = Object.keys(images).length > 0;
	const out = output ?? input.replace(/\.studypack\.json$|\.json$/, hasImg ? '.zip' : '.md');
	if (out.endsWith('.zip')) {
		const files: Record<string, Uint8Array> = { 'goi.md': strToU8(md) };
		for (const [p, uri] of Object.entries(images)) files[p] = Buffer.from(uri.split(',')[1], 'base64');
		writeFileSync(out, zipSync(files, { level: 6 }));
	} else writeFileSync(out, md, 'utf8');
	console.log(`→ ${out} (${Object.keys(images).length} ảnh)`);
} else {
	let md = '';
	const images: Record<string, string> = {};
	if (input.endsWith('.zip')) {
		for (const [p, data] of Object.entries(unzipSync(readFileSync(input)))) {
			if (p.endsWith('.md')) md = strFromU8(data);
			else if (mime(p)) images[p] = `data:${mime(p)};base64,${b64(data)}`;
		}
	} else md = readFileSync(input, 'utf8');
	const r = parseMarkdown(md, images);
	for (const e of r.errors) console.log(`  LỖI dòng ${e.line}: ${e.message}`);
	for (const w of r.warnings) console.log(`  cảnh báo dòng ${w.line}: ${w.message}`);
	const out = output ?? input.replace(/\.(md|zip)$/, '.studypack.json');
	writeFileSync(out, JSON.stringify(r.pack, null, 2) + '\n', 'utf8');
	console.log(`→ ${out}${r.errors.length ? ` (${r.errors.length} lỗi)` : ''}`);
	process.exit(r.errors.length ? 1 : 0);
}
