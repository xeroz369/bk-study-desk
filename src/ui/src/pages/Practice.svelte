<!-- Luyện tập: trang tổng (#luyen-tap) hoặc một môn (#luyen-tap/<môn>): bài theo chương + thi thử. -->
<script lang="ts">
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import StatStrip from '$lib/components/app/StatStrip.svelte';
	import DataRow from '$lib/components/app/DataRow.svelte';
	import Tag from '$lib/components/app/Tag.svelte';
	import { Progress as Bar } from '$lib/components/ui/progress';
	import { Button } from '$lib/components/ui/button';
	import * as Alert from '$lib/components/ui/alert';
	import { api } from '$lib/api/client';
	import type { PackIssue } from '$lib/study/pack';
	import { install, prepareImport, readPackFile } from '$lib/study/transfer';
	import { dayDiff, examTime, num } from '$lib/format';
	import { Study } from '$lib/study/registry.svelte';
	import { Progress, today } from '$lib/study/progress.svelte';
	import { STATUS_TEXT, type Question } from '$lib/study/types';
	import CalendarCheck from '@lucide/svelte/icons/calendar-check';
	import Shuffle from '@lucide/svelte/icons/shuffle';
	import Pencil from '@lucide/svelte/icons/pencil';

	let { arg }: { arg: string } = $props();
	const course = $derived(arg ? Study.course(arg) : undefined);
	const review = $derived(Progress.reviewCount());
	// Random exams live only in memory (results keep their title); list the real ones.
	const examsOf = (cid: string) => Object.values(Study.exams).filter((x) => x.courseId === cid && !x.id.startsWith('ngau-nhien-'));
	const due = $derived(Progress.dueQuestions(undefined, 999).length);
	/** Courses that can make a random exam: a real exam layout, or enough questions (10+). */
	const examable = $derived(
		Study.manifest.courses.filter(
			(c) => c.blueprint || Study.entries(c.id).reduce((n, e) => n + (Study.lessons[e.id]?.questions?.length ?? 0), 0) >= 10,
		),
	);
	// Not done yet or due today first, then the rest (randomness inside each group comes from randomExam).
	const fresh = (q: Question) => {
		const r = q.fp ? Progress.state.srs[q.fp] : undefined;
		return !r || r.due <= today() ? 0 : 1;
	};
	function randomExam(cid: string) {
		const x = Study.randomExam(cid, fresh);
		if (x) location.hash = '#thi/' + x.id;
		else notice = { title: 'Môn này chưa có câu nào để ra đề', lines: [] };
	}
	// Nhập quiz từ file (.md, .zip có ảnh, .json): file tự ghi môn/chương/bài nên vào đúng chỗ. GIFT, Moodle XML cần chọn môn: dùng Soạn → Nhập.
	let picker = $state<HTMLInputElement>();
	let notice = $state<{ title: string; lines: string[] } | null>(null);
	const issueText = (i: PackIssue) => (i.path ? `${i.path}: ` : '') + i.message;
	async function importFile(e: Event) {
		const input = e.currentTarget as HTMLInputElement;
		const f = input.files?.[0];
		input.value = '';
		if (!f) return;
		let file: { text: string; images: Record<string, string> };
		try {
			file = await readPackFile(f);
		} catch (x) {
			notice = { title: `Không đọc được ${f.name}`, lines: [x instanceof Error ? x.message : String(x)] };
			return;
		}
		const r = prepareImport(file.text, null, '', file.images);
		if (r.error || !r.pack || !r.report?.ok) {
			const errs = (r.report?.errors ?? []).map(issueText);
			notice = { title: r.error ?? `Không nhập được ${f.name}`, lines: [...r.notes, ...errs].slice(0, 8) };
			return;
		}
		if (
			Study.packs.some((x) => x.pack?.id === r.pack!.id) &&
			!confirm(`Đã có "${r.pack.title}". Cập nhật bằng file này? Kết quả luyện tập vẫn giữ nguyên.`)
		)
			return;
		await install(r.pack).catch((x) => (notice = { title: 'Không lưu được quiz', lines: [x instanceof Error ? x.message : String(x)] }));
	}
	async function removePack(id: string, title: string) {
		if (!confirm(`Gỡ gói "${title}"? Kết quả đã làm vẫn được giữ.`)) return;
		await api.deletePack(id);
		await Progress.flush();
		location.reload();
	}
	const packMeta = (x: (typeof Study.packs)[number]) =>
		x.pack && x.report?.ok
			? `${x.pack.course.name} · ${x.questions} câu · v${x.pack.version} · ${x.pack.authors.map((a) => a.name).join(', ')}${x.report.warnings.length ? ` · ${x.report.warnings.length} cảnh báo` : ''}`
			: x.report
				? `lỗi: ${issueText(x.report.errors[0])}`
				: `lỗi: ${x.error}`;
	// Only show a tag when there is something to say (in progress / done); "not started" is the default.
	const tone = (id: string) => (({ xong: 'ok', 'dang-hoc': 'warn' }) as Record<string, 'ok' | 'warn'>)[Progress.status(id)];
</script>

{#if !arg}
	<PageShell title="Luyện tập" meta="Tự soạn quiz, ôn tập, thi thử">
		{#snippet actions()}{#if Study.pages['lo-trinh']}<Button size="xs" variant="ghost" href="#trang/lo-trinh">Lộ trình ôn</Button
				>{/if}{/snippet}
	</PageShell>
	<div class="flex flex-col gap-stack">
		<div class="grid gap-3 sm:grid-cols-2">
			<Button size="lg" variant={due ? 'default' : 'outline'} class="h-auto justify-start py-3 text-left" href="#on-hom-nay">
				<CalendarCheck class="size-5" />
				<span class="flex flex-col">
					<span class="text-base font-semibold">Ôn hôm nay{due ? ` · ${due} câu` : ''}</span>
					<span class="text-xs font-normal opacity-80">{due ? 'câu hay sai lên trước' : 'chưa có câu cần ôn hôm nay'}</span>
				</span>
			</Button>
			<div class="flex flex-col gap-1.5 rounded-lg px-3 py-2 ring-1 ring-foreground/10">
				<span class="flex items-center gap-2 text-sm font-semibold"><Shuffle class="size-4" />Thi thử đề ngẫu nhiên</span>
				<span class="flex flex-wrap gap-1.5">
					{#each examable as c (c.id)}
						<Button
							size="sm"
							variant="outline"
							title={c.blueprint ? `${c.blueprint.minutes} phút, giống cấu trúc đề thật` : ''}
							onclick={() => randomExam(c.id)}>{c.name}</Button
						>
					{:else}<span class="text-xs text-muted-foreground">chưa có môn nào có câu hỏi</span>{/each}
				</span>
			</div>
		</div>
		<StatStrip
			items={[
				{ label: 'Câu sai', value: review, note: 'sai ở lần đầu', href: '#on-cau-sai', tone: review ? 'warn' : undefined },
				{ label: 'Đánh dấu', value: Progress.activeNotes().length, note: 'ghi chú · nghi sai', href: '#danh-dau' },
				{ label: 'Lần thi thử', value: Progress.state.exams.length, href: '#ket-qua' },
				{ label: 'Quiz LMS đã lưu', value: Study.quizInfo.length, note: 'kho quiz', href: '#kho-quiz' },
			]}
		/>
		<Panel title="Môn học">
			{#each Study.manifest.courses as c (c.id)}
				{@const s = Progress.courseStats(c.id)}
				<DataRow
					title={c.name}
					href={'#luyen-tap/' + c.id}
					sub={`${s.done}/${s.total} bài xong · đã soạn ${s.written}${examsOf(c.id).length ? ` · ${examsOf(c.id).length} đề thi thử` : ''}`}
					meta={c.exam ? `thi ${c.exam.date.slice(8)}/${c.exam.date.slice(5, 7)}` : ''}
				>
					{#snippet trailing()}<Bar class="h-1 w-24" value={s.total ? (s.done / s.total) * 100 : 0} />{/snippet}
				</DataRow>
			{/each}
		</Panel>
		{#if notice}
			<Alert.Root variant="destructive">
				<Alert.Title>{notice.title}</Alert.Title>
				{#if notice.lines.length}
					<Alert.Description
						><ul class="list-disc pl-4">
							{#each notice.lines as l, i (i)}<li>{l}</li>{/each}
						</ul></Alert.Description
					>
				{/if}
			</Alert.Root>
		{/if}
		<Panel title="Quiz tự soạn và đã nhập" meta="chuột phải để gỡ">
			{#snippet action()}
				<Button size="xs" variant="ghost" href="#soan">Soạn</Button>
				<Button size="xs" variant="ghost" onclick={() => picker?.click()}>Nhập file…</Button>
			{/snippet}
			{#each Study.packs as x (x.file)}
				<DataRow
					title={x.pack?.title ?? x.file}
					sub={packMeta(x)}
					dim={!x.report?.ok}
					menu={x.pack ? () => [{ label: 'Gỡ quiz này', run: () => removePack(x.pack!.id, x.pack!.title) }] : undefined}
				/>
			{:else}
				<DataRow title="Chưa có" sub="Soạn quiz của bạn hoặc Nhập file… (.md, .zip, .json)" dim />
			{/each}
		</Panel>
		<input bind:this={picker} type="file" accept=".md,.zip,.json" class="hidden" onchange={importFile} />
	</div>
{:else if course}
	{@const s = Progress.courseStats(course.id)}
	{@const d = course.exam ? dayDiff(examTime(course.exam.date, course.exam.time)) : null}
	<PageShell
		title={course.name}
		crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }]}
		meta={[
			course.exam
				? `thi ${course.exam.date.slice(8)}/${course.exam.date.slice(5, 7)} ${course.exam.time}${d !== null && d >= 0 ? ` (còn ${d} ngày)` : ''}`
				: '',
			course.scoring ? `${course.scoring.count} câu, đúng +${num(course.scoring.right, 3)}, sai −${num(-course.scoring.wrong, 3)}` : '',
			`${s.done}/${s.total} bài xong`,
		]
			.filter(Boolean)
			.join(' · ')}
	>
		{#snippet actions()}<Button size="xs" variant="ghost" href={`#soan/tao/${course.id}`}>Soạn · Nhập · Xuất</Button>{/snippet}
	</PageShell>
	<div class="flex flex-col gap-stack">
		{#each course.units as u, ui (u.title)}
			<Panel title={u.title} meta={u.pack && !u.pack.id.startsWith('quiz-lms') ? `của ${u.pack.authors}` : ''}>
				{#snippet action()}{#if !u.pack?.id.startsWith('quiz-lms')}<Button
							size="icon-xs"
							variant="ghost"
							title="Soạn, nhập, xuất chương này"
							aria-label="Soạn, nhập, xuất chương này"
							href={`#soan/tao/${course.id}/${ui}`}><Pencil /></Button
						>{/if}{/snippet}
				{#each u.lessons as e (e.id)}
					{@const l = Study.lessons[e.id]}
					{@const qs = l?.questions ?? []}
					<DataRow
						title={e.title}
						href={'#bai/' + e.id}
						dim={!l}
						meta={qs.length ? `${qs.filter((q) => Progress.q(q.id)?.correct).length}/${qs.length} câu` : ''}
					>
						{#snippet trailing()}{#if tone(e.id)}<Tag tone={tone(e.id)} text={STATUS_TEXT[Progress.status(e.id)]} />{/if}{/snippet}
					</DataRow>
				{/each}
			</Panel>
		{/each}
		{#if examsOf(course.id).length}
			<Panel title="Thi thử">
				{#snippet action()}<Button size="xs" variant="ghost" onclick={() => randomExam(course.id)}>Đề ngẫu nhiên</Button>{/snippet}
				{#each examsOf(course.id) as x (x.id)}
					{@const last = Progress.state.exams.filter((a) => a.examId === x.id).at(-1)}
					<DataRow
						title={x.title}
						sub={`${Study.examQuestions(x).length} câu · ${x.minutes} phút`}
						href={'#thi/' + x.id}
						meta={last ? `gần nhất ${num(last.score)}/${num(last.max)}` : 'chưa làm'}
					/>
				{/each}
			</Panel>
		{/if}
	</div>
{:else}
	<PageShell title="Không tìm thấy môn" crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }]} />
{/if}
