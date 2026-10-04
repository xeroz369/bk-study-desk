import './app.css';

// Nhúng trong app WPF: dùng màu nhấn của Windows (?accent=#RRGGBB) cho primary, nền trong suốt để hòa với thẻ của WPF.
const params = new URLSearchParams(location.search);
if (params.has('embed')) document.documentElement.classList.add('embed');
const accent = params.get('accent');
if (accent && /^#[0-9a-f]{6}$/i.test(accent)) {
	const s = document.documentElement.style;
	s.setProperty('--primary', accent);
	s.setProperty('--ring', accent);
	// Chữ trên nền nhấn: đen hay trắng theo độ sáng của màu nhấn.
	const [r, g, b] = [1, 3, 5].map((i) => parseInt(accent.slice(i, i + 2), 16));
	s.setProperty('--primary-foreground', 0.299 * r + 0.587 * g + 0.114 * b > 150 ? '#000000' : '#ffffff');
}

// Phông, cỡ chữ người dùng chọn trong Cài đặt của app (?font=<tên phông>&fs=<tỉ lệ>). Đổi khi khung đang mở thì app WPF gọi
// window.applyAppFont qua ExecuteScriptAsync, không tải lại trang. Chỉ đặt biến CSS trên :root (app.css: --app-font, --app-root-size);
// tỉ lệ không nhỏ hơn 1 để chữ nhỏ nhất vẫn 12px (MASTER).
declare global {
	interface Window {
		applyAppFont?: (family: string, scale: number) => void;
	}
}
function applyAppFont(family: string, scale: number) {
	const s = document.documentElement.style;
	if (family && /^[^"'\\;{}<>,]{1,100}$/.test(family)) s.setProperty('--app-font', `"${family}"`);
	else s.removeProperty('--app-font');
	const k = Number.isFinite(scale) ? Math.min(2, Math.max(1, scale)) : 1;
	if (k > 1) s.setProperty('--app-root-size', `${Math.round(16 * k * 2) / 2}px`);
	else s.removeProperty('--app-root-size');
}
window.applyAppFont = applyAppFont;
applyAppFont(params.get('font') ?? '', Number(params.get('fs') ?? '1'));
import { mount } from 'svelte';
import App from './App.svelte';

export default mount(App, { target: document.getElementById('app')! });
