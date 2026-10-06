<!-- Luyện tập: trang tổng (#luyen-tap) hoặc một môn (#luyen-tap/<môn>): bài theo chương + thi thử. -->
<script lang="ts">
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import StatStrip from '$lib/components/app/StatStrip.svelte';
	import DataRow from '$lib/components/app/DataRow.svelte';
	import EmptyRow from '$lib/components/app/EmptyRow.svelte';
	import SoanButtons from '$lib/components/app/SoanButtons.svelte';
	import Tag from '$lib/components/app/Tag.svelte';
	import { Progress as Bar } from '$lib/components/ui/progress';
	import { Button } from '$lib/components/ui/button';
	import * as Alert from '$lib/components/ui/alert';
	import { api, errorText } from '$lib/api/client';
	import type { PackIssue, StudyPack } from '$lib/study/pack';
	import { install, prepareImport, readPackFile } from '$lib/study/transfer';
	import {
		courseActions,
		courseCanMix,
		isLmsUnit,
		LABELS,
		lessonActions,
		packActions,
		startRandomExam,
		unitActions,
	} from '$lib/study/actions';
	import { dayDiff, examTime, num } from '$lib/format';
	import { canRandomExam, Study } from '$lib/study/registry.svelte';
	import { Progress } from '$lib/study/progress.svelte';
	import { STATUS_TEXT } from '$lib/study/types';
	import CalendarCheck from '@lucide/svelte/icons/calendar-check';
	import Shuffle from '@lucide/svelte/icons/shuffle';

	let { arg }: { arg: string } = $props();
	const course = $derived(arg ? Study.course(arg) : undefined);
	const review = $derived(Progress.reviewCount());
	// Random exams live only in memory (results keep their title); list the real ones.
	const examsOf = (cid: string) => Object.values(Study.exams).filter((x) => x.courseId === cid && !x.id.startsWith('ngau-nhien-'));
	const due = $derived(Progress.dueQuestions(undefined, 999).length);
	const examable = $derived(Study.manifest.courses.filter(canRandomExam));
	function randomExam(cid: string) {
		if (!startRandomExam(cid))
			notice = { title: 'Môn này chưa có câu nào để ra đề', lines: ['Tạo hoặc nhập câu hỏi cho môn này trước.'] };
	}
	// Nhập quiz từ file (.md, .zip có ảnh, .json): file tự ghi môn/chương/bài nên vào đúng chỗ. GIFT, Moodle XML cần chọn môn: dùng nút Nhập (trang Tạo quiz).
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
			notice = { title: `Không đọc được ${f.name}`, lines: [errorText(x)] };
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
		await install(r.pack).catch((x) => (notice = { title: 'Không lưu được quiz', lines: [errorText(x)] }));
	}
	async function removePack(id: string, title: string) {
		if (!confirm(`Gỡ gói "${title}"? Kết quả đã làm vẫn được giữ.`)) return;
		try {
			await api.deletePack(id);
		} catch (x) {
			notice = { title: `Chưa gỡ được "${title}"`, lines: [errorText(x)] };
			return;
		}
		await Progress.flush();
		location.reload();
	}
	const packMeta = (x: (typeof Study.packs)[number]) =>
		x.pack && x.report?.ok
			? `${x.pack.course.name}, ${x.questions} câu, v${x.pack.version}, ${x.pack.authors.map((a) => a.name).join(', ')}${x.report.warnings.length ? `, ${x.report.warnings.length} cảnh báo` : ''}`
			: x.report
				? `lỗi: ${issueText(x.report.errors[0])}`
				: `lỗi: ${x.error}`;
	const courseOfPack = (p: StudyPack) => Study.manifest.courses.find((c) => c.code?.toUpperCase() === p.course.code.trim().toUpperCase());
	// Only show a tag when there is something to say (in progress / done); "not started" is the default.
	const tone = (id: string) => (({ xong: 'ok', 'dang-hoc': 'warn' }) as Record<string, 'ok' | 'warn'>)[Progress.status(id)];
</script>

{#snippet noticeBox()}
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
{/snippet}

{#if !arg}
	<PageShell title="Luyện tập" meta="Tự tạo quiz, ôn tập, thi thử">
		{#snippet actions()}{#if Study.pages['lo-trinh']}<Button size="xs" variant="ghost" href="#trang/lo-trinh">Lộ trình ôn</Button
				>{/if}{/snippet}
	</PageShell>
	<div class="flex flex-col gap-stack">
		<div class="grid gap-3 sm:grid-cols-2">
			<Button size="lg" variant={due ? 'default' : 'outline'} class="h-auto justify-start py-3 text-left" href="#on-hom-nay">
				<CalendarCheck class="size-5" />
				<span class="flex flex-col">
					<span class="text-base font-semibold">Ôn hôm nay{due ? `: ${due} câu` : ''}</span>
					<span class="text-xs font-normal opacity-80">{due ? 'câu hay sai lên trước' : 'chưa có câu cần ôn hôm nay'}</span>
				</span>
			</Button>
			<div class="flex flex-col gap-1.5 rounded-lg px-3 py-2 ring-1 ring-foreground/10">
				<span class="flex items-center gap-2 text-sm font-semibold"><Shuffle class="size-4" />{LABELS.randomExam}</span>
				<span class="flex flex-wrap gap-1.5">
					{#each examable as c (c.id)}
						<Button
							size="sm"
							variant="outline"
							title={c.blueprint ? `${c.blueprint.minutes} phút, giống cấu trúc đề thật` : ''}
							onclick={() => randomExam(c.id)}>{c.name}</Button
						>
					{:else}<span class="text-xs text-muted-foreground">Cần một môn có từ 10 câu. Tạo hoặc nhập câu hỏi trước.</span>{/each}
				</span>
			</div>
		</div>
		<StatStrip
			items={[
				{ label: 'Câu sai', value: review, note: 'sai ở lần đầu', href: '#on-cau-sai', tone: review ? 'warn' : undefined },
				{ label: 'Đánh dấu', value: Progress.activeNotes().length, note: 'ghi chú, nghi sai', href: '#danh-dau' },
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
					menu={() => courseActions(c, randomExam)}
					sub={`${s.done}/${s.total} bài xong, đã tạo ${s.written}${examsOf(c.id).length ? `, ${examsOf(c.id).length} đề thi thử` : ''}`}
					meta={c.exam ? `thi ${c.exam.date.slice(8)}/${c.exam.date.slice(5, 7)}` : ''}
				>
					{#snippet trailing()}<Bar class="h-1 w-24" value={s.total ? (s.done / s.total) * 100 : 0} />{/snippet}
				</DataRow>
			{:else}
				<EmptyRow
					>Chưa có môn nào. Tạo quiz cho môn đang học, hoặc nhập file quiz bạn bè gửi.
					<Button size="xs" variant="ghost" href="#soan">{LABELS.button.create}</Button>
					<Button size="xs" variant="ghost" onclick={() => picker?.click()}>{LABELS.button.import}</Button></EmptyRow
				>
			{/each}
		</Panel>
		{@render noticeBox()}
		<Panel title="Quiz tự tạo và đã nhập" meta={Study.packs.length ? 'chuột phải để xem thêm' : ''}>
			{#snippet action()}
				<Button size="xs" variant="ghost" href="#soan">{LABELS.button.create}</Button>
				<Button size="xs" variant="ghost" onclick={() => picker?.click()}>Nhập file...</Button>
			{/snippet}
			{#each Study.packs as x (x.file)}
				{@const c = x.pack && x.report?.ok ? courseOfPack(x.pack) : undefined}
				<DataRow
					title={x.pack?.title ?? x.file}
					sub={packMeta(x)}
					dim={!x.report?.ok}
					href={c ? '#luyen-tap/' + c.id : undefined}
					menu={x.pack ? () => packActions(x.pack!, !!x.report?.ok, () => removePack(x.pack!.id, x.pack!.title)) : undefined}
				/>
			{:else}
				<EmptyRow>Chưa có quiz nào. Chọn Tạo để tự soạn, hoặc Nhập file... (.md, .zip, .json).</EmptyRow>
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
			course.scoring
				? `${course.scoring.count} câu, đúng +${num(course.scoring.right, 3)}, sai −${num(-course.scoring.wrong, 3)}`
				: '',
			`${s.done}/${s.total} bài xong`,
		]
			.filter(Boolean)
			.join(', ')}
	>
		{#snippet actions()}{#if courseCanMix(course)}<Button size="sm" variant="outline" href={'#luyen-tron/' + course.id}
					>{LABELS.mix}</Button
				>{/if}<SoanButtons path={course.id} where="môn này" />{/snippet}
	</PageShell>
	<div class="flex flex-col gap-stack">
		{@render noticeBox()}
		{#each course.units as u, ui (u.title)}
			<Panel
				title={u.title}
				meta={u.pack && !isLmsUnit(u) ? `của ${u.pack.authors}` : ''}
				menu={isLmsUnit(u) ? undefined : () => unitActions(course, ui)}
			>
				{#snippet action()}{#if !isLmsUnit(u)}<SoanButtons path={`${course.id}/${ui}`} where="chương này" />{/if}{/snippet}
				{#each u.lessons as e (e.id)}
					{@const l = Study.lessons[e.id]}
					{@const qs = l?.questions ?? []}
					<DataRow
						title={e.title}
						href={'#bai/' + e.id}
						menu={() => lessonActions(e.id)}
						dim={!l}
						meta={qs.length ? `${qs.filter((q) => Progress.q(q.id)?.correct).length}/${qs.length} câu` : ''}
					>
						{#snippet trailing()}{#if tone(e.id)}<Tag
									tone={tone(e.id)}
									text={STATUS_TEXT[Progress.status(e.id)]}
								/>{/if}{/snippet}
					</DataRow>
				{/each}
			</Panel>
		{/each}
		{#if examsOf(course.id).length || canRandomExam(course)}
			<Panel title="Thi thử">
				{#snippet action()}{#if canRandomExam(course)}<Button size="xs" variant="ghost" onclick={() => randomExam(course.id)}
							>{LABELS.randomExam}</Button
						>{/if}{/snippet}
				{#each examsOf(course.id) as x (x.id)}
					{@const last = Progress.state.exams.filter((a) => a.examId === x.id).at(-1)}
					<DataRow
						title={x.title}
						sub={`${Study.examQuestions(x).length} câu, ${x.minutes} phút`}
						href={'#thi/' + x.id}
						meta={last ? `gần nhất ${num(last.score)}/${num(last.max)}` : 'chưa làm'}
					/>
				{:else}
					<EmptyRow>Môn này chưa có đề thi thử soạn sẵn. Chọn {LABELS.randomExam} để làm đề rút từ câu trong bài.</EmptyRow>
				{/each}
			</Panel>
		{/if}
	</div>
{:else}
	<PageShell title="Không tìm thấy môn" crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }]} />
{/if}
