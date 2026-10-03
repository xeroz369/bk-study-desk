<!-- Soạn và chia sẻ (#soan/<tab>/<môn>[/<chương>[/<bài>]]): Tạo · Nhập · Xuất · Nhờ AI, luôn gắn với đúng chỗ đang đứng.
     Câu/bài tự soạn lưu vào gói tu-soan-<mã môn> (content/packs) nên xuất, chia sẻ được như gói khác. -->
<script lang="ts">
	import { RECALL_PREFIX, UNIT_TITLE } from '$lib/study/lmsquiz';
	import { onMount } from 'svelte';
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import DataRow from '$lib/components/app/DataRow.svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import * as Alert from '$lib/components/ui/alert';
	import { copy } from '$lib/menu.svelte';
	import { api, errorText } from '$lib/api/client';
	import { Desktop } from '$lib/desktop.svelte';
	import { normText } from '$lib/study/text';
	import { typeset } from '$lib/math';
	import { Study } from '$lib/study/registry.svelte';
	import QuestionEditor from '$lib/components/app/QuestionEditor.svelte';
	import { countQuestions, validatePack, type PackIssue, type PackQuestion, type StudyPack } from '$lib/study/pack';
	import { toGift, toMoodleXml } from '$lib/study/formats';
	import { writeMarkdown } from '$lib/study/markdown';
	import { buildPrompt, type PromptMode } from '$lib/study/prompts';
	import {
		authoredPack,
		buildExport,
		bumpVersion,
		download,
		downloadText,
		exportMarkdown,
		install,
		lessonIn,
		lessonOf,
		parseScope,
		prepareImport,
		questionMarkdown,
		readPackFile,
		scopeLabel,
		scopePath,
		slug,
		unitOf,
		type Prepared,
	} from '$lib/study/transfer';

	let { arg }: { arg: string } = $props();
	const TABS = [
		['tao', 'Tạo'],
		['nhap', 'Nhập'],
		['xuat', 'Xuất'],
		['ai', 'Nhờ AI'],
	] as const;
	const tab = $derived((TABS.find(([k]) => arg.split('/')[0] === k)?.[0] ?? 'tao') as string);
	const rest = $derived(TABS.some(([k]) => arg.split('/')[0] === k) ? arg.split('/').slice(1).join('/') : arg);
	const scope = $derived(rest ? parseScope(rest) : null);
	const unit = $derived(scope ? unitOf(scope) : undefined);
	const lesson = $derived(scope ? lessonOf(scope) : undefined);
	const level = $derived(lesson ? 'bài' : unit ? 'chương' : scope ? 'môn' : '');
	const tabs = $derived(TABS.map(([k, label]) => [`${k}${rest ? '/' + rest : ''}`, label] as [string, string]));
	const crumbs = $derived([
		{ label: 'Luyện tập', href: '#luyen-tap' },
		...(scope ? [{ label: scope.course.name, href: '#luyen-tap/' + scope.course.id }] : []),
		...(lesson ? [{ label: lesson.title, href: '#bai/' + lesson.id }] : []),
	]);

	// Tên tác giả: nhớ trên máy này (chỉ để ghi vào gói).
	let author = $state('');
	let flash = $state('');
	onMount(() => {
		try {
			author = localStorage.getItem('studypack.author') ?? '';
		} catch {
			/* trình duyệt chặn storage */
		}
		flash = sessionStorage.getItem('studypack.flash') ?? '';
		sessionStorage.removeItem('studypack.flash');
		// From the quiz archive: "Ghi nhanh câu còn nhớ" pre-fills the chapter and lesson names.
		// Giá trị hỏng (sửa tay, bản cũ) thì bỏ qua, không làm hỏng cả trang.
		let pre: { unit?: unknown; lesson?: unknown } | null = null;
		try {
			pre = JSON.parse(sessionStorage.getItem('soan.prefill') ?? 'null');
		} catch {
			pre = null;
		}
		sessionStorage.removeItem('soan.prefill');
		if (pre && typeof pre.unit === 'string' && typeof pre.lesson === 'string') {
			newUnit = pre.unit;
			newLesson = pre.lesson;
		}
	});
	$effect(() => {
		try {
			localStorage.setItem('studypack.author', author);
		} catch {
			/* bỏ qua */
		}
	});
	const issue = (i: PackIssue) => (i.path ? `${i.path}: ` : '') + i.message;

	// ------------------------------------------------------------------ Chọn môn: môn đang học (API) + môn đã có trong sổ
	let subjects = $state<{ code: string; name: string }[]>([]);
	onMount(async () => {
		try {
			subjects = await api.subjects();
		} catch {
			/* chưa sync: chỉ còn môn trong sổ */
		}
	});
	const pickList = $derived.by(() => {
		const out = subjects.map((s) => ({ ...s, course: Study.manifest.courses.find((c) => c.code?.toUpperCase() === s.code.toUpperCase()) }));
		for (const c of Study.manifest.courses)
			if (!out.some((x) => x.course === c)) out.push({ code: c.code ?? c.id.toUpperCase(), name: c.name, course: c });
		return out;
	});
	function pickCourse(code: string, name: string) {
		const c = Study.ensureCourse(code, name);
		location.hash = `#soan/${tab}/${c.id}`;
	}

	// ------------------------------------------------------------------ Tạo
	let newLesson = $state('');
	let newUnit = $state('');
	let body = $state('');
	let createErrors = $state<string[]>([]);
	const mine = $derived(scope && lesson ? (Study.lessons[lesson.id]?.questions ?? []).filter((q) => q.packId === authoredId()) : []);
	// Gợi ý trong ô nhập tính theo môn đang chọn, không gắn với một môn cụ thể.
	const unitHint = $derived(
		`Chương ${(scope?.course.units.filter((u) => !u.pack?.id.startsWith('quiz-lms')).length ?? 0) + 1}: tên chương`,
	);
	const lessonHint = $derived(`Bài ${(unit?.lessons.length ?? 0) + 1}: tên bài`);
	const BODY_HINT = 'Tóm tắt kiến thức. Công thức viết bằng TeX, ví dụ \\( x^2 \\) trong dòng hoặc \\[ \\frac{a}{b} \\] riêng dòng';
	/** Writing into "Quiz LMS đã lưu" = recalling a quiz with no review: saved in the locked recall pack. */
	const recall = $derived((unit?.title ?? newUnit.trim()) === UNIT_TITLE);
	function authoredId() {
		return scope ? slug(`${recall ? RECALL_PREFIX : 'tu-soan-'}${scope.course.code ?? scope.course.id.toUpperCase()}`) : '';
	}

	/** Validate then save the authored pack; returns errors (empty = saved, page reloads).
	 *  hashAfter chỉ được áp sau khi app đã lưu xong (install), lưu lỗi thì vẫn ở trang hiện tại. */
	async function saveAuthored(p: StudyPack, hashAfter?: string): Promise<string[]> {
		const r = validatePack(p);
		if (!r.ok) return (createErrors = r.errors.map(issue));
		bumpVersion(p);
		try {
			await install(p, hashAfter);
		} catch (e) {
			const text = `Chưa lưu được: ${errorText(e)}. Thử lại sau giây lát.`;
			Desktop.inform({ id: 'soan-save', tone: 'bad', text });
			return (createErrors = [text]);
		}
		return [];
	}
	async function saveQuestion(q: PackQuestion): Promise<string[]> {
		if (!scope || !unit || !lesson) return ['Chưa chọn bài.'];
		const p = authoredPack(scope.course, author, recall);
		const l = lessonIn(p, unit.title, lesson.title);
		l.questions ??= [];
		const used = new Set(l.questions.map((x) => x.id));
		let n = l.questions.length + 1;
		while (used.has(`q${n}`)) n++;
		l.questions.push({ id: `q${n}`, ...q });
		return saveAuthored(p);
	}
	async function removeQuestion(qid: string) {
		if (!scope || !unit || !lesson || !confirm('Xóa câu tự tạo này?')) return;
		const p = authoredPack(scope.course, author, recall);
		const l = lessonIn(p, unit.title, lesson.title);
		const local = qid.split('.').at(-1);
		l.questions = (l.questions ?? []).filter((q) => q.id !== local);
		const errs = await saveAuthored(p);
		if (errs.length) Desktop.inform({ id: 'soan-save', tone: 'bad', text: `Không xóa được câu: ${errs[0]}` });
	}
	async function addLesson() {
		if (!scope) return;
		const unitTitle = unit?.title ?? newUnit.trim();
		if (!unitTitle || !newLesson.trim()) {
			createErrors = ['Cần tên chương và tên bài.'];
			return;
		}
		const p = authoredPack(scope.course, author, recall);
		const l = lessonIn(p, unitTitle, newLesson.trim());
		if (body.trim()) l.sections = [...(l.sections ?? []), { title: 'Kiến thức', body: body.trim() }];
		if (!l.sections?.length && !l.questions?.length) l.sections = [{ title: 'Ghi chú', body: '<p>Bài mới, chưa có nội dung.</p>' }];
		// Chương, bài trùng tên (đã chuẩn hóa) thì gói được ghép vào đó lúc nạp (registry.addPack): mở đúng chỗ đó.
		// Chỉ số chương có thể lệch sau khi tải lại (thứ tự gói); parseScope tự tìm lại chương theo id bài.
		const same = (a: string, b: string) => normText(a) === normText(b);
		const found = scope.course.units.findIndex((u) => same(u.title, unitTitle));
		const ui = unit ? scope.unit! : found >= 0 ? found : scope.course.units.length;
		const host = scope.course.units[ui]?.lessons.find((e) => same(e.title, l.title));
		await saveAuthored(p, `#soan/tao/${scope.course.id}/${ui}/${host?.id ?? `${p.id}.${l.id}`}`);
	}

	// ------------------------------------------------------------------ Nhập
	let raw = $state('');
	let rawImages = $state<Record<string, string>>({});
	let picker = $state<HTMLInputElement>();
	const prepared = $derived<Prepared | null>(raw.trim() ? prepareImport(raw, scope, author, rawImages) : null);
	async function pickFile(e: Event) {
		const input = e.currentTarget as HTMLInputElement;
		const f = input.files?.[0];
		input.value = '';
		if (!f) return;
		try {
			const r = await readPackFile(f);
			rawImages = r.images;
			raw = r.text;
			Desktop.dismiss('soan-import');
		} catch (e) {
			Desktop.inform({ id: 'soan-import', tone: 'bad', text: `Không đọc được ${f.name}: ${errorText(e)}` });
		}
	}
	async function doImport() {
		const p = prepared?.pack;
		if (!p || !prepared?.report?.ok) return;
		if (Study.packs.some((x) => x.pack?.id === p.id) && !confirm(`Đã có gói "${p.id}". Thay bằng gói này?`)) return;
		try {
			await install(p);
		} catch (e) {
			Desktop.inform({ id: 'soan-import', tone: 'bad', text: `Chưa nhập được: ${errorText(e)}. Thử lại sau giây lát.` });
		}
	}

	// ------------------------------------------------------------------ Xuất
	const exported = $derived(scope ? buildExport(scope, author) : null);
	const exportReport = $derived(exported ? validatePack(exported) : null);
	const exportOff = $derived(!exportReport?.ok || !exportReport?.stats.lessons);

	// ------------------------------------------------------------------ Nhờ AI
	const MODES: [PromptMode, string, string][] = [
		['soan', 'Tạo mới', 'từ slide, đề bạn gửi kèm'],
		['tuong-tu', 'Câu tương tự', 'đổi số liệu từ một câu có sẵn'],
		['soat', 'Soát đáp án', 'AI kiểm lại các câu ở đây'],
		['giai-thich', 'Giải thích', 'một câu bạn chưa hiểu'],
	];
	let mode = $state<PromptMode>('soan');
	let count = $state(10);
	let extra = $state('');
	let pickedQ = $state('');
	/** Questions at this place (for "câu tương tự" / "giải thích"). */
	const scopeQuestions = $derived(
		scope
			? (lesson ? [lesson] : unit ? unit.lessons : scope.course.units.flatMap((u) => u.lessons)).flatMap(
					(e) => Study.lessons[e.id]?.questions ?? [],
				)
			: [],
	);
	const promptText = $derived.by(() => {
		if (!scope) return '';
		const code = scope.course.code ?? scope.course.id.toUpperCase();
		const q = scopeQuestions.find((x) => x.id === pickedQ);
		const qmd = q ? questionMarkdown(q) : undefined;
		return buildPrompt(mode, {
			course: `${code} ${scope.course.name}`,
			unit: unit?.title,
			lesson: lesson?.title,
			units: scope.course.units.map((u) => u.title),
			count,
			extra,
			question: qmd,
			pack: mode === 'soat' && exported ? writeMarkdown(exported).md : undefined,
		});
	});
</script>

<PageShell
	title={scope ? `Tạo quiz, ${scopeLabel(scope)}` : 'Tạo quiz'}
	{crumbs}
	tabs={scope ? tabs : []}
	tab={`${tab}${rest ? '/' + rest : ''}`}
	base="soan"
	meta={level ? `đang ở ${level}` : 'chọn môn'}
/>

<div class="flex flex-col gap-stack">
	{#if flash}
		<Alert.Root><Alert.Title>{flash}</Alert.Title></Alert.Root>
	{/if}

	{#if !scope}
		<Panel title="Ba cách tạo quiz" pad>
			<ul class="list-disc space-y-1 pl-5 text-sm">
				<li><b>Tự tạo:</b> chọn môn, chương, bài rồi gõ câu hỏi (dán ảnh bằng Ctrl+V).</li>
				<li><b>Nhờ AI:</b> ở tab <i>Nhờ AI</i>, sao chép prompt, rồi dán kết quả AI trả về vào tab <i>Nhập</i>.</li>
				<li><b>Nhập file:</b> sang tab <i>Nhập</i> rồi chọn file .md, .zip, Moodle XML, GIFT, Aiken. Lỗi báo đúng dòng.</li>
			</ul>
		</Panel>
		<Panel title="Chọn môn" meta="môn đang học kỳ này (LMS, MyBK) và môn đã có bài">
			{#each pickList as c (c.code + c.name)}
				<DataRow
					title={c.name}
					sub={c.code + (c.course ? `, ${c.course.units.reduce((n, u) => n + u.lessons.length, 0)} bài` : ', chưa có bài')}
					onclick={() => pickCourse(c.code, c.name)}
				/>
			{:else}
				<DataRow title="Chưa có môn nào" sub="Đồng bộ LMS hoặc MyBK trước để có danh sách môn đang học" dim />
			{/each}
		</Panel>
	{:else if tab === 'tao'}
		{#if lesson && unit}
			<Panel title="Câu hỏi mới" meta={`vào bài "${lesson.title}", chương "${unit.title}"`} pad>
				<QuestionEditor onsave={saveQuestion} />
				<label class="mt-3 flex flex-col gap-1 text-sm">Tên bạn (ghi vào gói)<Input bind:value={author} /></label>
			</Panel>
			{#if mine.length}
				<Panel title="Câu bạn đã tạo trong bài này" meta="chuột phải để xóa">
					{#each mine as q (q.id)}
						<DataRow
							title={q.prompt.replace(/<[^>]+>/g, '').slice(0, 120)}
							sub={q.tag ?? ''}
							menu={() => [{ label: 'Xóa câu', run: () => removeQuestion(q.id) }]}
						/>
					{/each}
				</Panel>
			{/if}
		{:else}
			{#if !unit}
				<Panel title="Chọn chương để tạo câu" meta="hoặc tạo chương mới bên dưới">
					{#each scope.course.units as u, i (i)}
						<DataRow title={u.title} sub={`${u.lessons.length} bài`} href={`#soan/tao/${scope.course.id}/${i}`} />
					{/each}
				</Panel>
			{:else}
				<Panel title="Chọn bài để thêm câu hỏi">
					{#each unit.lessons as e (e.id)}
						<DataRow title={e.title} href={`#soan/tao/${scope.course.id}/${scope.unit}/${e.id}`} />
					{/each}
				</Panel>
			{/if}
			<Panel title={unit ? 'Bài mới trong chương này' : 'Chương mới'} pad>
				<div class="flex flex-col gap-3 text-sm">
					{#if !unit}<label class="flex flex-col gap-1">Tên chương<Input bind:value={newUnit} placeholder={unitHint} /></label>{/if}
					<label class="flex flex-col gap-1">Tên bài<Input bind:value={newLesson} placeholder={lessonHint} /></label>
					<label class="flex flex-col gap-1"
						>Kiến thức (không bắt buộc, Markdown + TeX)<Textarea bind:value={body} rows={4} placeholder={BODY_HINT} /></label
					>
					<label class="flex flex-col gap-1">Tên bạn (ghi vào gói)<Input bind:value={author} /></label>
					{#if createErrors.length}
						<Alert.Root variant="destructive"
							><Alert.Title>Chưa lưu được</Alert.Title><Alert.Description
								><ul class="list-disc pl-4">
									{#each createErrors as e, i (i)}<li>{e}</li>{/each}
								</ul></Alert.Description
							></Alert.Root
						>
					{/if}
					<Button class="self-start" onclick={addLesson}>Tạo bài, rồi thêm câu hỏi</Button>
				</div>
			</Panel>
		{/if}
	{:else if tab === 'nhap'}
		<Panel
			title={`Nhập vào ${level} "${scopeLabel(scope)}"`}
			meta="Markdown (.md, .zip có ảnh), Moodle XML, GIFT, Aiken hoặc gói .json"
			pad
		>
			<div class="flex flex-col gap-3 text-sm">
				<Textarea
					bind:value={raw}
					oninput={() => (rawImages = {})}
					rows={8}
					placeholder={'Dán khối Markdown AI trả về hoặc nội dung file...\n\n### Câu 1\nĐề bài\n- [x] đúng\n- [ ] sai\n> lời giải'}
					class="font-mono text-xs"
				/>
				<div class="flex gap-2">
					<Button size="sm" variant="outline" onclick={() => picker?.click()}>Chọn file...</Button>
					<Button size="sm" onclick={doImport} disabled={!prepared?.report?.ok}>Nhập vào {level} này</Button>
				</div>
				<input bind:this={picker} type="file" accept=".md,.zip,.json,.xml,.txt,.gift" class="hidden" onchange={pickFile} />
				{#if prepared?.error}
					<Alert.Root variant="destructive">
						<Alert.Title>{prepared.error}</Alert.Title>
						{#if prepared.notes.length}<Alert.Description
								><ul class="list-disc pl-4">
									{#each prepared.notes as n, i (i)}<li>{n}</li>{/each}
								</ul></Alert.Description
							>{/if}
					</Alert.Root>
					<p class="text-xs text-muted-foreground">Nếu do AI tạo: sao chép các lỗi trên, dán lại cho AI để sửa.</p>
				{:else if prepared?.report}
					{@const r = prepared.report}
					<Alert.Root variant={r.ok ? 'default' : 'destructive'}>
						<Alert.Title
							>{r.ok
								? `Hợp lệ: ${r.stats.lessons} bài, ${r.stats.questions} câu, ${r.stats.exams} đề`
								: `${r.errors.length} lỗi, chưa nhập được`}</Alert.Title
						>
						<Alert.Description>
							<ul class="list-disc pl-4">
								{#each prepared.notes as n, i (i)}<li>{n}</li>{/each}
								{#each r.errors.slice(0, 8) as e, i (i)}<li>{issue(e)}</li>{/each}
								{#each r.warnings.slice(0, 5) as w, i (i)}<li class="text-muted-foreground">cảnh báo: {issue(w)}</li>{/each}
							</ul>
						</Alert.Description>
					</Alert.Root>
				{/if}
			</div>
		</Panel>
	{:else if tab === 'xuat' && exported && exportReport}
		<Panel title={`Xuất ${level} "${scopeLabel(scope)}"`} pad>
			<div class="flex flex-col gap-3 text-sm">
				<p>
					{exportReport.stats.lessons} bài, {countQuestions(exported)} câu, {exportReport.stats.exams} đề. Người nhận nhập vào sẽ vào đúng chương,
					bài.
				</p>
				<label class="flex flex-col gap-1">Tên bạn (ghi vào gói)<Input bind:value={author} /></label>
				{#if !exportReport.ok}
					<Alert.Root variant="destructive"
						><Alert.Title>Gói xuất ra còn lỗi</Alert.Title><Alert.Description
							><ul class="list-disc pl-4">
								{#each exportReport.errors.slice(0, 6) as e, i (i)}<li>{issue(e)}</li>{/each}
							</ul></Alert.Description
						></Alert.Root
					>
				{/if}
				<div class="flex flex-wrap gap-2">
					<Button size="sm" onclick={() => exportMarkdown(exported)} disabled={exportOff}>Lưu Markdown (.md / .zip)...</Button>
					<Button size="sm" variant="outline" onclick={() => copy(writeMarkdown(exported).md, 'Đã sao chép Markdown')} disabled={exportOff}
						>Sao chép Markdown</Button
					>
				</div>
				<div class="flex flex-wrap gap-2">
					<Button
						size="xs"
						variant="ghost"
						onclick={() => downloadText(`${exported.id}.xml`, toMoodleXml(exported), 'application/xml')}
						disabled={exportOff}>Moodle XML (đưa lên LMS)</Button
					>
					<Button
						size="xs"
						variant="ghost"
						onclick={() => downloadText(`${exported.id}.gift.txt`, toGift(exported), 'text/plain')}
						disabled={exportOff}>GIFT</Button
					>
					<Button size="xs" variant="ghost" onclick={() => download(exported)} disabled={exportOff}>JSON</Button>
				</div>
				<p class="text-xs text-muted-foreground">
					Gửi file cho bạn qua Zalo hoặc Messenger. Bạn ấy mở <b>Luyện tập</b> > <b>Nhập file...</b> để nhập. Moodle XML dùng để đưa lên ngân
					hàng câu hỏi LMS.
				</p>
			</div>
		</Panel>
	{:else if tab === 'ai'}
		<Panel title="Nhờ AI" meta={`kết quả sẽ vào đúng ${level} "${scopeLabel(scope)}"`} pad>
			<div class="flex flex-col gap-3 text-sm">
				<div class="flex flex-wrap gap-1.5" role="radiogroup" aria-label="Việc nhờ AI">
					{#each MODES as [m, label, hint] (m)}
						<Button
							size="xs"
							variant={mode === m ? 'default' : 'outline'}
							role="radio"
							aria-checked={mode === m}
							title={hint}
							onclick={() => (mode = m)}>{label}</Button
						>
					{/each}
				</div>
				{#if mode === 'tuong-tu' || mode === 'giai-thich'}
					<div class="flex flex-col gap-1">
						<span>Chọn câu {mode === 'tuong-tu' ? 'mẫu' : 'cần giải thích'}</span>
						<div class="max-h-48 overflow-y-auto rounded-md border">
							{#each scopeQuestions.slice(0, 200) as q (q.id)}
								<DataRow
									title={q.prompt
										.replace(/<[^>]+>/g, '')
										.replace(/\\[()[\]]/g, '')
										.slice(0, 110)}
									sub={q.tag ?? ''}
									strong={pickedQ === q.id}
									onclick={() => (pickedQ = q.id)}
								/>
							{:else}
								<DataRow title="Chưa có câu nào ở đây" dim />
							{/each}
						</div>
					</div>
				{/if}
				<ol class="list-decimal pl-5">
					<li>Sao chép prompt, dán vào ChatGPT / Claude / Gemini{mode === 'soan' ? ', kèm slide hoặc đề (PDF, ảnh)' : ''}.</li>
					{#if mode === 'giai-thich'}<li>Đọc lời giải thích của AI.</li>{:else}<li>
							AI trả về một khối Markdown: sao chép hết, sang tab <a class="link" href={`#soan/nhap/${scopePath(scope)}`}>Nhập</a>, dán vào
							rồi chọn Nhập. Có lỗi thì dán lỗi lại cho AI sửa.
						</li>{/if}
				</ol>
				{#if mode === 'soan' || mode === 'tuong-tu'}
					<div class="flex flex-wrap items-end gap-3">
						<label class="flex flex-col gap-1">Số câu<Input type="number" min={1} max={40} bind:value={count} class="w-24" /></label>
						<label class="flex min-w-64 flex-1 flex-col gap-1"
							>Yêu cầu thêm (không bắt buộc)<Input
								bind:value={extra}
								placeholder="Ví dụ: tập trung câu bấm máy, mức khó như đề giữa kỳ"
							/></label
						>
					</div>
				{/if}
				<Textarea value={promptText} rows={12} readonly class="font-mono text-xs" />
				<Button
					size="sm"
					class="self-start"
					disabled={(mode === 'tuong-tu' || mode === 'giai-thich') && !pickedQ}
					onclick={() => copy(promptText, 'Đã sao chép prompt')}>Sao chép prompt</Button
				>
			</div>
		</Panel>
	{/if}
</div>
