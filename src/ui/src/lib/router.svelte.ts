// Định tuyến bằng #hash: #tên/tham-số. Danh sách trang ở lib/routes.ts.

function parse(hash: string) {
	const [name, ...rest] = hash.replace(/^#/, '').split('/');
	return { name: name || 'luyen-tap', arg: rest.join('/') };
}

/** Vùng nội dung tự cuộn (cửa sổ không cuộn). */
export const contentEl = () => document.getElementById('content');

class Router {
	route = $state(parse(location.hash));

	constructor() {
		window.addEventListener('hashchange', () => {
			const next = parse(location.hash);
			if (next.name !== this.route.name) contentEl()?.scrollTo({ top: 0 });
			this.route = next;
		});
	}
}

export const router = new Router();
