// Công thức toán: MathJax (≈2 MB, có sẵn trong vendor/) chỉ tải lần đầu gặp nội dung có TeX.
// Dùng: <div use:typeset={nội dung}>…</div> — chạy lại khi tham số đổi.

interface MathJaxApi {
	typesetPromise(el: Element[]): Promise<void>;
	typesetClear?(el: Element[]): void;
	startup?: { promise: Promise<void> };
}
declare global {
	interface Window {
		MathJax?: MathJaxApi | Record<string, unknown>;
	}
}

let loading: Promise<MathJaxApi | null> | null = null;
const HAS_TEX = /\\\(|\\\[/;

function mathJax() {
	return (loading ??= new Promise<MathJaxApi | null>((resolve) => {
		window.MathJax = {
			// No \href, \style, \class, \require: pack content must not get links, styles or extra code through TeX.
			tex: {
				inlineMath: [['\\(', '\\)']],
				displayMath: [['\\[', '\\]']],
				packages: { '[-]': ['html', 'require'] },
				autoload: { html: [], require: [] },
			},
			svg: { fontCache: 'global' },
			startup: { typeset: false },
		};
		const s = document.createElement('script');
		s.src = 'vendor/mathjax/tex-svg.js';
		s.onload = async () => {
			const mj = window.MathJax as MathJaxApi;
			await mj.startup?.promise;
			resolve(mj);
		};
		s.onerror = () => resolve(null);
		document.head.appendChild(s);
	}));
}

async function run(el: HTMLElement) {
	if (!HAS_TEX.test(el.innerHTML)) return;
	const mj = await mathJax();
	if (!mj || !el.isConnected) return;
	mj.typesetClear?.([el]);
	await mj.typesetPromise([el]).catch(() => {});
}

export function typeset(el: HTMLElement, _dep?: unknown) {
	void run(el);
	return { update: () => queueMicrotask(() => void run(el)) };
}
