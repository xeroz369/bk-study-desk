// Kiểm thử đầu-cuối khung Luyện tập trên app thật qua DevTools Protocol: đi qua mọi trang, chạy các luồng chính, gom lỗi console.
// Cách chạy: mở app với WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9334 (đang ở trang Luyện tập), rồi
//   npm run e2e            (đổi cổng: CDP_PORT=9333 npm run e2e)
// In PASS/FAIL từng bước; có FAIL hoặc lỗi console thì thoát với mã 1. Bài, môn, đề lấy theo dữ liệu đang có trong app.
const port = process.env.CDP_PORT ?? 9334;
const list = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
const page = list.find((t) => t.type === 'page' && t.url.includes('sohoc.app'));
const ws = new WebSocket(page.webSocketDebuggerUrl);
let id = 0;
const pending = new Map();
const errors = [];
ws.onmessage = (m) => {
	const d = JSON.parse(m.data);
	if (d.id && pending.has(d.id)) return pending.get(d.id)(d);
	if (d.method === 'Runtime.exceptionThrown')
		errors.push('exception: ' + (d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text).split('\n')[0]);
	if (d.method === 'Runtime.consoleAPICalled' && ['error', 'warning'].includes(d.params.type))
		errors.push(
			d.params.type +
				': ' +
				d.params.args
					.map((a) => a.value ?? a.description)
					.join(' ')
					.slice(0, 200),
		);
};
await new Promise((r) => (ws.onopen = r));
const call = (method, params = {}) =>
	new Promise((r) => {
		const i = ++id;
		pending.set(i, r);
		ws.send(JSON.stringify({ id: i, method, params }));
	});
await call('Runtime.enable');
const ev = async (expr) => {
	const r = await call('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: true });
	if (r.result?.exceptionDetails) return 'EVAL ERROR: ' + (r.result.exceptionDetails.exception?.description ?? '').split('\n')[0];
	return r.result?.result?.value;
};
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const go = async (h) => {
	await ev(`location.hash = ${JSON.stringify('#' + h)}`);
	await sleep(1300);
};
const out = [];
const check = (name, ok, info = '') => out.push(`${ok ? 'PASS' : 'FAIL'}  ${name}${info ? '  · ' + info : ''}`);

// 1. every route renders a title, no "Không tìm thấy"
const courses = await ev('Study.manifest.courses.map(c => c.id)');
const lessons = await ev('Study.entries().filter(e => Study.lessons[e.id]).slice(0, 4).map(e => e.id)');
const exams = await ev("Object.keys(Study.exams).filter(x => !x.startsWith('ngau-nhien'))");
const routes = [
	'luyen-tap',
	...courses.map((c) => 'luyen-tap/' + c),
	...lessons.map((l) => 'bai/' + l),
	...exams.map((x) => 'thi/' + x),
	'on-cau-sai',
	'on-hom-nay',
	'danh-dau',
	'ket-qua',
	'kho-quiz',
	'soan',
	`soan/tao/${courses[0]}`,
	`soan/nhap/${courses[0]}`,
	`soan/xuat/${courses[0]}`,
	`soan/ai/${courses[0]}`,
];
for (const r of routes) {
	await go(r);
	const t = await ev("document.querySelector('h1')?.innerText ?? ''");
	check('route #' + r, !!t && !/Không tìm thấy/.test(t), t);
}

// 2. lesson: answer first question (practice), result text appears
await go('bai/' + lessons[0]);
check(
	'lesson answer',
	await ev(
		`(async () => { const q = document.querySelector('.question'); const b = q.querySelector('.grid button'); b.click(); await new Promise(r => setTimeout(r, 400)); return /Đúng\\.|Chưa đúng/.test(q.innerText); })()`,
	),
);

// 3. exam: sticky bar opaque, pick, submit twice, result shows score + times (bỏ qua khi app chưa có đề thi thử)
if (exams.length) {
	await go('thi/' + exams[0]);
	const bar = await ev(
		`(() => { const b = document.querySelector('.sticky'); const c = getComputedStyle(b).backgroundColor; const m = c.match(/[\\d.]+/g).map(Number); return { c, alpha: m.length > 3 ? m[3] : 1 }; })()`,
	);
	check('exam sticky bar opaque', bar.alpha >= 0.99, bar.c);
	check(
		'exam submit',
		await ev(`(async () => {
	const qs = [...document.querySelectorAll('.question')];
	for (const q of qs.slice(0, 3)) { q.querySelector('.grid button')?.click(); await new Promise(r => setTimeout(r, 300)); }
	const sub = () => [...document.querySelectorAll('button')].find(b => /Nộp bài|Bấm lần nữa/.test(b.innerText));
	sub().click(); await new Promise(r => setTimeout(r, 150)); sub().click(); await new Promise(r => setTimeout(r, 700));
	return /Điểm/.test(document.querySelector('main')?.innerText ?? document.body.innerText);
})()`),
	);
}

// 4. random exam per course with questions
for (const c of courses) {
	const r = await ev(
		`(() => { const x = Study.randomExam(${JSON.stringify(c)}); return x ? Study.examQuestions(x).length + ' câu / ' + x.minutes + ' phút' : null; })()`,
	);
	check('random exam ' + c, r === null || /^\d+ câu/.test(r), String(r));
}

// 5. today review + flag + note via card buttons
await go('on-hom-nay');
const due = await ev("document.querySelectorAll('.question').length");
check('today review lists due', typeof due === 'number', due + ' câu');
await go('bai/' + lessons[0]);
check(
	'flag toggle',
	await ev(
		`(async () => { const q = document.querySelector('.question'); const b = () => [...q.querySelectorAll('button')].find(x => /nghi sai|Nghi đáp án sai/i.test(x.innerText)); b().click(); await new Promise(r => setTimeout(r, 300)); const on = /Bỏ cờ/.test(b().innerText); b().click(); await new Promise(r => setTimeout(r, 300)); return on && !/Bỏ cờ/.test(b().innerText); })()`,
	),
);

// 6. import pasted bare questions: at a lesson → placed there (button enabled); at course level → hint shown
const paste = async (route) => {
	await go(route);
	return ev(`(async () => {
		const ta = document.querySelector('textarea');
		ta.value = '### Câu 1\\n1 + 1 = ?\\n- [x] 2\\n- [ ] 3\\n> hiển nhiên';
		ta.dispatchEvent(new Event('input', { bubbles: true }));
		await new Promise(r => setTimeout(r, 500));
		const b = [...document.querySelectorAll('button')].find(x => x.innerText.startsWith('Nhập vào'));
		return { enabled: !!b && !b.disabled, text: document.body.innerText };
	})()`);
};
const atLesson = await paste(`soan/nhap/${courses[0]}/0/${lessons[0]}`);
check('import bare questions into a lesson', atLesson.enabled);
const atCourse = await paste(`soan/nhap/${courses[0]}`);
check('import bare questions at course: hint', !atCourse.enabled && /Mẹo: chọn một chương hoặc bài/.test(atCourse.text));

// 7. export tab builds markdown without error
await go(`soan/xuat/${courses[0]}`);
check('export tab', (await ev("document.querySelector('main')?.innerText ?? document.body.innerText")).includes('Markdown'));

// 8. kho quiz switch exists and shows state
await go('kho-quiz');
check('auto-save switch', !!(await ev("document.querySelector('[data-slot=switch]')?.getAttribute('data-state')")));

// 9. results page lists attempts
await go('ket-qua');
check('results', /Lịch sử thi thử/.test(await ev('document.body.innerText')));

// 10. horizontal overflow on narrow width
await call('Emulation.setDeviceMetricsOverride', { width: 760, height: 700, deviceScaleFactor: 1, mobile: false });
for (const r of ['luyen-tap', 'kho-quiz', ...(exams.length ? ['thi/' + exams[0]] : []), 'bai/' + lessons[0]]) {
	await go(r);
	const w = await ev(
		"(() => { const c = document.getElementById('content') ?? document.scrollingElement; return [c.scrollWidth, c.clientWidth]; })()",
	);
	check('no h-scroll at 760px #' + r, w[0] <= w[1] + 1, w.join('/'));
}
await call('Emulation.clearDeviceMetricsOverride');
await go('luyen-tap');

console.log(out.join('\n'));
console.log(errors.length ? '\nCONSOLE/EXCEPTIONS:\n' + [...new Set(errors)].join('\n') : '\nno console errors');
process.exit(out.some((l) => l.startsWith('FAIL')) || errors.length ? 1 : 0);
ws.close();
