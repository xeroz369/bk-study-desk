// Lọc HTML của gói luyện tập trước khi hiển thị ({@html}). Gói có thể đến từ người khác nên không tin nội dung:
// chỉ giữ thẻ định dạng trong ALLOWED_TAGS, bỏ mọi thuộc tính trừ class (lọc theo ALLOWED_CLASSES) và colspan/rowspan.
// Thẻ lạ thì bỏ thẻ, giữ chữ bên trong; script/style/iframe… thì bỏ cả nội dung.

import { ALLOWED_CLASSES, ALLOWED_TAGS, IMG_SRC } from './pack';

const DROP_WITH_CONTENT = new Set([
	'script',
	'style',
	'iframe',
	'object',
	'embed',
	'template',
	'noscript',
	'svg',
	'math',
	'video',
	'audio',
	'form',
	'input',
	'textarea',
	'button',
	'select',
	'link',
	'meta',
	'base',
]);

export function sanitizeHtml(html: string): string {
	const doc = new DOMParser().parseFromString(`<body>${html}</body>`, 'text/html');
	clean(doc.body);
	return doc.body.innerHTML;
}

function clean(node: Element) {
	for (const child of [...node.children]) {
		const tag = child.tagName.toLowerCase();
		if (DROP_WITH_CONTENT.has(tag)) {
			child.remove();
			continue;
		}
		clean(child);
		if (!ALLOWED_TAGS.has(tag)) {
			child.replaceWith(...child.childNodes);
			continue;
		}
		if (tag === 'img' && !IMG_SRC.test(child.getAttribute('src') ?? '')) {
			child.remove(); // ảnh link ngoài / SVG: bỏ hẳn
			continue;
		}
		for (const a of [...child.attributes]) {
			const name = a.name.toLowerCase();
			if (
				tag === 'img' &&
				(name === 'src' || name === 'alt' || ((name === 'width' || name === 'height') && /^\d{1,4}$/.test(a.value)))
			)
				continue;
			if (name === 'class') {
				const keep = a.value.split(/\s+/).filter((c) => ALLOWED_CLASSES.has(c));
				if (keep.length) child.setAttribute('class', keep.join(' '));
				else child.removeAttribute('class');
			} else if ((name === 'colspan' || name === 'rowspan') && /^\d{1,2}$/.test(a.value)) {
				// giữ cho bảng
			} else child.removeAttribute(a.name);
		}
	}
}
