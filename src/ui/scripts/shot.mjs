// Chụp một trang của app đang chạy (cổng DevTools 9333). node scripts/shot.mjs '#lms' out/lms.png [rộng=1280] [light|dark]
const [hash, out, width = '1280', scheme = 'light'] = process.argv.slice(2);
const list = await (await fetch('http://127.0.0.1:9333/json')).json();
const ws = new WebSocket(list.find(p => p.url.includes('sohoc.example')).webSocketDebuggerUrl);
await new Promise(r => ws.onopen = r);
let id = 0; const pending = {};
ws.onmessage = m => { const d = JSON.parse(m.data); if (pending[d.id]) pending[d.id](d); };
const call = (method, params = {}) => new Promise(r => { pending[++id] = r; ws.send(JSON.stringify({ id, method, params })); });
await call('Emulation.setDeviceMetricsOverride', { width: +width, height: 900, deviceScaleFactor: 1, mobile: false });
await call('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: scheme }] });
await call('Runtime.evaluate', { expression: `location.hash = ${JSON.stringify(hash)}` });
await new Promise(r => setTimeout(r, 2000));
const s = await call('Page.captureScreenshot', { format: 'png' });
(await import('node:fs')).writeFileSync(out, Buffer.from(s.result.data, 'base64'));
await call('Emulation.setEmulatedMedia', { features: [] });
ws.close();
