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
import { mount } from 'svelte';
import App from './App.svelte';

export default mount(App, { target: document.getElementById('app')! });
