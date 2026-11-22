
interface Hashable {
	type?: string;
	prompt: string;
	options?: string[];
	answer?: number | boolean;
	answers?: number[];
	value?: number;
	accept?: string[];
}

export function fpText(html: string): string {
	return html
		.replace(/<img\b[^>]*>/gi, ' [img] ')
		.replace(/<[^>]+>/g, ' ')
		.replace(/&nbsp;/g, ' ')
		.replace(/&lt;/g, '<')
		.replace(/&gt;/g, '>')
		.replace(/&amp;/g, '&')
		.normalize('NFC')
		.toLowerCase()
		.replace(/\\[,;: !]/g, '')
		.replace(/\s+/g, '');
}

function hash(s: string): string {
	let a = 0x811c9dc5,
		b = 0x01000193 ^ 0x5bd1e995;
	for (let i = 0; i < s.length; i++) {
		const c = s.charCodeAt(i);
		a = Math.imul(a ^ c, 0x01000193) >>> 0;
		b = Math.imul(b ^ c, 0x5bd1e995) >>> 0;
	}
	return a.toString(16).padStart(8, '0') + b.toString(16).padStart(8, '0');
}

export function fingerprint(q: Hashable): string {
	const opts = (q.options ?? []).map(fpText).sort();
	const key =
		q.type === 'numeric' ? String(q.value ?? q.answer) : q.type === 'short' ? (q.accept ?? []).map(fpText).sort().join('|') : '';
	return hash([fpText(q.prompt), ...opts, key].join('\u0001'));
}
