// Kiểm tra UI tự động (design-system/so-hoc-tap/MASTER.md mục 7) — chạy trên app thật qua DevTools Protocol.
// Gồm cả luật ứng dụng Windows (DESIGN.md mục 7): cửa sổ không cuộn, con trỏ mũi tên, khung không bôi chọn chữ.
// Cách chạy: mở app với WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333, rồi
//   npm run audit                       → 1280/900 × sáng/tối, mọi trang
//   node scripts/audit-ui.mjs 900 light lms mybk/ctdt   → một cấu hình, vài trang
// Kết quả: in số lỗi theo loại; chi tiết ở scripts/out/audit-<rộng>-<chế độ>.json. Bàn giao khi mọi cấu hình = {}.
import fs from 'node:fs';
fs.mkdirSync(new URL('./out/', import.meta.url), { recursive: true });
const [width = '1280', scheme = 'dark', ...only] = process.argv.slice(2);
// Mặc định chỉ các trang có ở cả hai bản; trang bài học, đề thi cụ thể thì truyền tên trang qua tham số.
const PAGES = only.length ? only : ['luyen-tap', 'on-cau-sai', 'ket-qua'];

const list = await (await fetch('http://127.0.0.1:9333/json')).json();
// Bản 1.x: host ảo sohoc.example; bản 2.0: máy chủ cục bộ 127.0.0.1 (index.html?embed=1&host=2).
const ws = new WebSocket(list.find((p) => p.url.includes('sohoc.example') || p.url.includes('host=2')).webSocketDebuggerUrl);
await new Promise((r) => (ws.onopen = r));
let id = 0; const pending = {}; const errors = [];
ws.onmessage = (m) => { const d = JSON.parse(m.data); if (pending[d.id]) pending[d.id](d);
  if (d.method === 'Runtime.exceptionThrown') errors.push(d.params.exceptionDetails.exception?.description?.slice(0, 200));
  if (d.method === 'Runtime.consoleAPICalled' && ['error', 'warning'].includes(d.params.type)) errors.push(d.params.args.map((a) => a.value ?? a.description).join(' ').slice(0, 200)); };
const call = (method, params = {}) => new Promise((r) => { pending[++id] = r; ws.send(JSON.stringify({ id, method, params })); });
await call('Runtime.enable');
await call('Emulation.setDeviceMetricsOverride', { width: +width, height: 900, deviceScaleFactor: 1, mobile: false });
await call('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: scheme }] });
// Embedded in WPF the theme is the .dark class + the Windows accent (tuned for that theme) set by the host,
// not the media query. Checking the other theme: flip the class and fall back to the app's own accent.
const FLIP = `(() => { const d = document.documentElement; if (d.classList.contains('dark') !== ${scheme === 'dark'}) { d.classList.toggle('dark'); for (const p of ['--primary', '--ring', '--primary-foreground']) d.style.removeProperty(p); } })()`;
await call('Runtime.evaluate', { expression: FLIP });

const CHECK = String.raw`(() => {
  const out = [];
  const vis = (el) => { const r = el.getBoundingClientRect(); const s = getComputedStyle(el); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none' && +s.opacity > 0.05; };
  const label = (el) => (el.getAttribute('aria-label') || el.getAttribute('title') || el.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 50);
  const where = (el) => { const p = el.closest('section,header,aside,nav,[data-slot=sidebar]'); return (p ? (p.querySelector('span,h1')?.textContent || p.tagName).trim().slice(0, 30) + ' â€º ' : '') + el.tagName.toLowerCase(); };
  const cv = document.createElement('canvas').getContext('2d', { willReadFrequently: true }); const parse = (c) => { cv.clearRect(0,0,1,1); cv.fillStyle = '#000'; cv.fillStyle = c; cv.fillRect(0,0,1,1); const d = cv.getImageData(0,0,1,1).data; return [d[0], d[1], d[2], d[3]/255]; };
  const lum = ([r,g,b]) => { const f = (v) => { v /= 255; return v <= 0.03928 ? v/12.92 : ((v+0.055)/1.055) ** 2.4; }; return 0.2126*f(r)+0.7152*f(g)+0.0722*f(b); };
  const bgOf = (el) => { let e = el; let acc = null; while (e) { const c = parse(getComputedStyle(e).backgroundColor); const a = c.length > 3 ? c[3] : 1; if (a > 0.5) return c.slice(0,3); e = e.parentElement; } const b = parse(getComputedStyle(document.body).backgroundColor); return b[3] > 0.5 ? b.slice(0,3) : (document.documentElement.classList.contains('dark') ? [32,32,32] : [243,243,243]); }; // embed: transparent page over the WPF Mica backdrop
  // 1. cá»­a sá»• khÃ´ng cuá»™n (chá»‰ vÃ¹ng ná»™i dung tá»± cuá»™n), vÃ¹ng ná»™i dung khÃ´ng cuá»™n ngang
  const de = document.documentElement;
  if (de.scrollHeight > innerHeight + 1 || de.scrollWidth > innerWidth + 1) out.push(['window-scroll', 'Cá»­a sá»• cuá»™n: ' + de.scrollWidth + 'Ã—' + de.scrollHeight]);
  const content = document.getElementById('content');
  if (content && content.scrollWidth > content.clientWidth + 1) out.push(['overflow-page', 'VÃ¹ng ná»™i dung cuá»™n ngang: ' + content.scrollWidth + '/' + content.clientWidth + 'px']);
  const seen = new Set();
  for (const el of document.querySelectorAll('main *, [data-slot=sidebar] *, #toolbar *, #statusbar *')) {
    if (!vis(el) || el.closest('mjx-container, mjx-assistive-mml, svg')) continue;
    const s = getComputedStyle(el);
    const ownText = [...el.childNodes].some((n) => n.nodeType === 3 && n.textContent.trim());
    // 2. chá»¯ < 12px
    if (ownText && parseFloat(s.fontSize) < 11.9) out.push(['small-text', parseFloat(s.fontSize).toFixed(1) + 'px: "' + label(el) + '" (' + where(el) + ')']);
    // 3. tÆ°Æ¡ng pháº£n
    if (ownText && !el.closest('[data-slot=tooltip-content]')) {
      const fg = parse(s.color); const fa = fg.length > 3 ? fg[3] : 1; const bg = bgOf(el);
      const mix = fg.slice(0,3).map((v,i) => v*fa + bg[i]*(1-fa)); const op = +s.opacity * (el.closest('[style*=opacity],.opacity-55,.opacity-60') ? 0.55 : 1);
      const L1 = lum(mix), L2 = lum(bg); const ratio = (Math.max(L1,L2)+0.05)/(Math.min(L1,L2)+0.05);
      const big = parseFloat(s.fontSize) >= 18.6 || (parseFloat(s.fontSize) >= 14 && +s.fontWeight >= 700);
      const need = big ? 3 : 4.5;
      const dimmed = !!el.closest('.opacity-55, .opacity-60, [data-dim]');
      if (ratio < need && !dimmed) out.push(['contrast', ratio.toFixed(2) + ':1 "' + label(el) + '" (' + where(el) + ')']);
    }
    // 5. bá»‹ cáº¯t khÃ´ng cÃ³ tooltip
    if (s.textOverflow === 'ellipsis' && el.scrollWidth > el.clientWidth + 1 && !el.title && !el.closest('[title]')) out.push(['truncated-no-title', '"' + label(el) + '" (' + where(el) + ')']);
    // 4. trÃ n khung cha
    if (el.scrollWidth > el.clientWidth + 2 && s.overflowX === 'visible' && s.textOverflow !== 'ellipsis' && el.children.length === 0 && ownText && !seen.has(label(el))) { seen.add(label(el)); out.push(['overflow-el', '"' + label(el) + '" (' + where(el) + ')']); }
  }
  for (const el of document.querySelectorAll('button, a[href], [role=button], [role=tab]')) {
    if (!vis(el) || el.dataset.sidebar === 'rail') continue;
    const s = getComputedStyle(el);
    // 6. khÃ´ng cÃ³ tÃªn
    if (!label(el) && !el.querySelector('img[alt]:not([alt=""])')) out.push(['no-name', where(el) + ' ' + (el.className || '').toString().slice(0, 60)]);
    // 7. con trá» kiá»ƒu Windows: mÅ©i tÃªn trÃªn nÃºt, hÃ ng, tab; bÃ n tay chá»‰ cho liÃªn káº¿t trong vÄƒn báº£n (.prose-lesson, .lms-html, a.link)
    const hyperlink = el.matches('a.link, .prose-lesson a, .lms-html a, .lms-question a');
    if (hyperlink ? s.cursor !== 'pointer' : s.cursor === 'pointer') out.push(['cursor', '"' + label(el) + '" (' + where(el) + ') cursor=' + s.cursor]);
    // 7b. khung giao diá»‡n khÃ´ng bÃ´i chá»n chá»¯ Ä‘Æ°á»£c
    if (!hyperlink && s.userSelect !== 'none' && !el.closest('.prose-lesson, .lms-html, .lms-question, .selectable')) out.push(['user-select', '"' + label(el) + '" (' + where(el) + ')']);
    // 8. lá»“ng nhau
    if (el.parentElement?.closest('button, a[href]')) out.push(['nested-interactive', '"' + label(el) + '" (' + where(el) + ')']);
    // 9. vÃ¹ng báº¥m quÃ¡ nhá» (web: 24x24 CSS px)
    const r = el.getBoundingClientRect(); if ((r.width < 24 || r.height < 20) && !el.closest('p, .prose-lesson, .lms-html')) out.push(['target-small', Math.round(r.width) + 'x' + Math.round(r.height) + ' "' + label(el) + '" (' + where(el) + ')']);
  }
  // 10. focus ring bá»‹ táº¯t
  return JSON.stringify(out);
})()`;

const report = {};
for (const p of PAGES) {
  errors.length = 0;
  await call('Runtime.evaluate', { expression: `location.hash = ${JSON.stringify('#' + p)}` });
  await new Promise((r) => setTimeout(r, 1800));
  await call('Runtime.evaluate', { expression: FLIP }); // host re-applies its theme on navigation
  const res = await call('Runtime.evaluate', { expression: CHECK, returnByValue: true });
  const issues = JSON.parse(res.result.result.value ?? '[]');
  if (errors.length) issues.push(...errors.map((e) => ['console', e]));
  report[p] = issues;
}
await call('Emulation.setEmulatedMedia', { features: [] });
await call('Emulation.clearDeviceMetricsOverride');
ws.close();
const file = new URL(`./out/audit-${width}-${scheme}.json`, import.meta.url);
fs.writeFileSync(file, JSON.stringify(report, null, 1));
const byKind = {};
for (const [p, list] of Object.entries(report)) for (const [k] of list) byKind[k] = (byKind[k] ?? 0) + 1;
console.log(width, scheme, JSON.stringify(byKind));
if (Object.keys(byKind).length) process.exitCode = 1;
