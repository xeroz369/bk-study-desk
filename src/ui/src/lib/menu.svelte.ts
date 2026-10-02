// Menu chuột phải của app: mỗi hàng đăng ký danh sách lệnh bằng use:menu; một ContextMenu duy nhất (App.svelte)
// hiện lệnh của hàng dưới con trỏ. Thứ tự theo Windows: lệnh mặc định (in đậm) · sao chép · lệnh theo nguồn.
// Ngoài hàng thì không có menu (ô nhập, chữ đang chọn: WebView2 hiện Cắt/Sao chép/Dán).

import { Desktop } from './desktop.svelte';

export interface MenuItem {
	label: string;
	run?: () => void;
	/** lệnh mặc định (bấm đúp, Enter) — in đậm, đứng đầu */
	primary?: boolean;
	hint?: string;
	disabled?: boolean;
	/** vạch chia đứng trước mục này */
	sep?: boolean;
}

const registry = new WeakMap<Element, () => MenuItem[]>();

/** Gắn menu cho một phần tử: {@attach menu(() => [...])} (chuyển được qua component shadcn). */
export function menu(items: () => MenuItem[]) {
	return (node: HTMLElement) => {
		registry.set(node, items);
		node.dataset.menu = '';
		return () => registry.delete(node);
	};
}

/** Lệnh của phần tử gần nhất có menu, hoặc null. */
export function menuFor(target: EventTarget | null): { el: HTMLElement; items: MenuItem[] } | null {
	const el = (target as Element | null)?.closest<HTMLElement>('[data-menu]');
	const get = el && registry.get(el);
	return el && get ? { el, items: get() } : null;
}

export async function copy(text: string, what = 'Đã sao chép') {
	try {
		await navigator.clipboard.writeText(text);
		Desktop.say(what);
	} catch {
		Desktop.say('Không sao chép được');
	}
}
