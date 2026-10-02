// Hành vi kiểu ứng dụng Windows (DESIGN.md mục 7): cỡ chữ vùng nội dung, thanh trạng thái, InfoBar, tìm trong trang,
// tìm khắp app, phím tắt. Trình duyệt đã tắt phím tắt/zoom/menu riêng (Shell/MainForm.cs) nên app tự lo các việc này.

import { untrack } from 'svelte';

const ZOOMS = [0.8, 0.9, 1, 1.1, 1.25, 1.5];
const KEY_ZOOM = 'so-hoc-zoom';

function load(key: string) {
	try {
		return localStorage.getItem(key);
	} catch {
		return null;
	}
}
function save(key: string, v: string) {
	try {
		localStorage.setItem(key, v);
	} catch {
		/* bỏ qua: chỉ là tiện ích */
	}
}

export interface InfoMessage {
	id: string;
	tone: 'info' | 'warn' | 'bad';
	text: string;
	action?: { label: string; run: () => void };
	/** gọi thêm khi người dùng đóng thanh */
	onclose?: () => void;
}

class DesktopState {
	/** Tỉ lệ phóng vùng nội dung (thanh bên, thanh công cụ, thanh trạng thái giữ cỡ theo Windows). */
	zoom = $state(Number(load(KEY_ZOOM)) || 1);
	/** Câu ngắn ở thanh trạng thái, tự mất sau vài giây. */
	status = $state('');
	/** Thanh InfoBar trong bố cục (không nổi đè nội dung). */
	infos = $state<InfoMessage[]>([]);
	findOpen = $state(false);
	#statusTimer = 0;

	setZoom(z: number) {
		this.zoom = Math.min(ZOOMS.at(-1)!, Math.max(ZOOMS[0], z));
		save(KEY_ZOOM, String(this.zoom));
	}
	zoomStep(dir: 1 | -1) {
		const i = ZOOMS.findIndex((z) => z >= this.zoom - 0.001);
		this.setZoom(ZOOMS[Math.min(ZOOMS.length - 1, Math.max(0, (i < 0 ? 2 : i) + dir))]);
		this.say(`Cỡ chữ ${Math.round(this.zoom * 100)}%`);
	}

	say(text: string, ms = 4000) {
		this.status = text;
		clearTimeout(this.#statusTimer);
		this.#statusTimer = window.setTimeout(() => (this.status = ''), ms);
	}

	// untrack: gọi được trong $effect mà không tự kích lại chính nó.
	inform(m: InfoMessage) {
		this.infos = [...untrack(() => this.infos).filter((x) => x.id !== m.id), m];
	}
	dismiss(id: string) {
		const list = untrack(() => this.infos);
		if (list.some((x) => x.id === id)) this.infos = list.filter((x) => x.id !== id);
	}
}

export const Desktop = new DesktopState();

/** Báo app WPF (khung này nhúng trong trang Luyện tập): phím của app, chuyển trang. */
export function toApp(message: Record<string, unknown>) {
	(window as unknown as { chrome?: { webview?: { postMessage(m: unknown): void } } }).chrome?.webview?.postMessage(message);
}

const isEditable = (el: Element | null) => !!el?.closest('input, textarea, select, [contenteditable=""], [contenteditable=true]');

/** Hàng trong danh sách: ↑/↓/Home/End chuyển giữa các hàng cùng khối (data-rows), như list view của Windows. */
function moveRow(e: KeyboardEvent) {
	const row = (document.activeElement as HTMLElement | null)?.closest<HTMLElement>('[data-row]');
	const list = row?.closest<HTMLElement>('[data-rows]');
	if (!row || !list) return false;
	const rows = [...list.querySelectorAll<HTMLElement>('[data-row]')].filter((r) => r.closest('[data-rows]') === list && r.offsetParent);
	const i = rows.indexOf(row);
	const to = { ArrowDown: i + 1, ArrowUp: i - 1, Home: 0, End: rows.length - 1 }[e.key];
	if (to == null || !rows[to]) return true;
	rows[to].focus();
	rows[to].scrollIntoView({ block: 'nearest' });
	return true;
}

/** Phím trong khung Luyện tập. Phím của app (Ctrl+1…6, F5) chuyển cho app WPF; danh sách ở Cài đặt của app. */
export function onKey(e: KeyboardEvent) {
	const ctrl = e.ctrlKey && !e.altKey;
	const editing = isEditable(e.target as Element);
	// Phím của app WPF (WPF không nhận phím khi khung HTML đang có focus): chuyển trang, đồng bộ, quay lại.
	if (e.key === 'F5' || (ctrl && /^[1-7]$/.test(e.key))) {
		e.preventDefault();
		toApp({ type: 'key', key: e.key === 'F5' ? 'F5' : 'Ctrl+' + e.key });
	} else if (ctrl && (e.key === 'f' || e.key === 'F')) {
		e.preventDefault();
		Desktop.findOpen = true;
		queueMicrotask(() => document.getElementById('find-input')?.focus());
	} else if (ctrl && (e.key === '=' || e.key === '+')) {
		e.preventDefault();
		Desktop.zoomStep(1);
	} else if (ctrl && e.key === '-') {
		e.preventDefault();
		Desktop.zoomStep(-1);
	} else if (ctrl && e.key === '0') {
		e.preventDefault();
		Desktop.setZoom(1);
		Desktop.say('Cỡ chữ 100%');
	} else if (e.altKey && !e.ctrlKey && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) {
		e.preventDefault();
		// Lùi trong khung trước; hết lịch sử của khung thì app WPF lùi trang.
		if (e.key === 'ArrowRight') history.forward();
		else if (history.length > 1) history.back();
		else toApp({ type: 'key', key: 'Alt+Left' });
	} else if (e.key === 'Escape' && Desktop.findOpen) {
		Desktop.findOpen = false;
	} else if (!editing && !e.ctrlKey && !e.altKey && ['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(e.key)) {
		if (moveRow(e)) e.preventDefault();
	}
}

/** Ctrl+lăn chuột trên vùng nội dung: đổi cỡ chữ (trình duyệt không còn tự zoom). */
export function onWheel(e: WheelEvent) {
	if (!e.ctrlKey) return;
	e.preventDefault();
	Desktop.zoomStep(e.deltaY < 0 ? 1 : -1);
}
