<!-- Thi thử: đếm giờ, không báo đúng sai tới khi nộp, chấm theo luật trừ điểm của môn, lưu vào kết quả.
     Thời gian từng câu = khoảng từ lần chọn trước tới lần chọn câu này; so với nhịp đề (phút × 60 / số câu). -->
<script lang="ts">
	import { onDestroy, untrack } from 'svelte';
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import StatStrip from '$lib/components/app/StatStrip.svelte';
	import QuestionCard from '$lib/components/app/QuestionCard.svelte';
	import SourceList from '$lib/components/app/SourceList.svelte';
	import { Button } from '$lib/components/ui/button';
	import { clock, num } from '$lib/format';
	import { Study } from '$lib/study/registry.svelte';
	import { Progress } from '$lib/study/progress.svelte';
	import { contentEl } from '$lib/router.svelte';
	import { isBlank, isCorrect } from '$lib/study/grade';
	import type { Answer } from '$lib/study/types';
	import ShuffleToggles from '$lib/components/app/ShuffleToggles.svelte';
	import { EXAM_SHUFFLE, loadPrefs, optionOrder, rng, seedFor } from '$lib/study/shuffle';
	import { shuffleGroups } from '$lib/study/exam';

	let { arg }: { arg: string } = $props();
	// Trang được dựng lại khi đổi địa chỉ ({#key} trong App), nên đọc đề một lần khi mở.
	const exam = untrack(() => Study.exams[arg]);
	const course = exam ? Study.course(exam.courseId) : undefined;
	const qs = exam ? Study.examQuestions(exam) : [];
	const sc = exam?.scoring ?? course?.scoring ?? { count: qs.length, right: 1, wrong: 0 };

	let picked = $state<Record<string, Answer>>({});
	// Shuffle like a real exam; answers are keyed by question id and original option index, so grading is unchanged.
	const seed = Date.now();
	let prefs = $state(loadPrefs({ questions: !!exam?.shuffle?.questions, options: !!exam?.shuffle?.options }, EXAM_SHUFFLE));
	// Câu cùng nhóm (dùng chung đề) vẫn đứng liền nhau khi xáo.
	const shown = $derived(prefs.questions ? shuffleGroups(qs, rng(seed)) : qs);
	const orders = $derived(
		Object.fromEntries(shown.map((q) => [q.id, prefs.options ? optionOrder(q, rng(seedFor(seed, q.id))) : undefined])),
	);
	// Countdown from a fixed deadline: timers are throttled when the page is hidden or the PC sleeps, so never count ticks.
	const deadline = Date.now() + (exam?.minutes ?? 0) * 60_000;
	let left = $state((exam?.minutes ?? 0) * 60);
	let armed = $state(false);
	let result = $state<null | { right: number; wrong: number; blank: number; score: number; max: number; seconds: number }>(null);
	const started = new Date();
	const pace = exam && qs.length ? (exam.minutes * 60) / qs.length : 0;
	let times = $state<Record<string, number>>({});
	let lastAt = started.getTime();
	function pick(id: string, a: Answer | null) {
		const now = Date.now();
		times[id] = Math.round((times[id] ?? 0) + (now - lastAt) / 1000);
		lastAt = now;
		if (a === null) delete picked[id];
		else picked[id] = a;
	}
	let disarm: ReturnType<typeof setTimeout> | undefined;

	const tick = setInterval(() => {
		left = Math.max(0, Math.round((deadline - Date.now()) / 1000));
		if (left <= 0) finish();
	}, 1000);
	onDestroy(() => {
		clearInterval(tick);
		clearTimeout(disarm);
	});

	function submit() {
		if (!armed) {
			armed = true;
			disarm = setTimeout(() => (armed = false), 4000);
			return;
		}
		finish();
	}

	function finish() {
		if (result || !exam) return;
		clearInterval(tick);
		let right = 0,
			wrong = 0,
			blank = 0;
		for (const q of qs) {
			const p = picked[q.id];
			const ok = !isBlank(p) && isCorrect(q, p);
			if (isBlank(p)) blank++;
			else if (ok) right++;
			else wrong++;
			// Review schedule: blank counts as not known.
			if (q.fp) Progress.srsAnswer(q.fp, ok);
			if (q.fp && times[q.id]) Progress.recordTime(q.fp, times[q.id]);
		}
		const score = right * sc.right + wrong * sc.wrong;
		const max = qs.length * sc.right;
		const seconds = Math.round((Date.now() - started.getTime()) / 1000);
		Progress.addExamAttempt({
			examId: exam.id,
			title: exam.title,
			startedAt: started.toISOString(),
			seconds,
			answers: { ...picked },
			times: { ...times },
			right,
			wrong,
			blank,
			score: Math.round(score * 1000) / 1000,
			max,
		});
		result = { right, wrong, blank, score, max, seconds };
		contentEl()?.scrollTo({ top: 0 });
	}
</script>

{#if !exam || !course}
	<PageShell title="Không tìm thấy đề" crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }]} />
{:else}
	<PageShell
		title={exam.title}
		crumbs={[{ label: course.name, href: '#luyen-tap/' + course.id }]}
		meta={`${qs.length} câu, ${exam.minutes} phút, đúng +${num(sc.right, 3)}, sai −${num(-sc.wrong, 3)}, trống 0`}
	/>
	<p class="mb-2 text-xs text-muted-foreground">Nộp bài mới biết đúng sai. Chọn lại đáp án đã chọn để bỏ trống.</p>
	<SourceList sources={exam.sources} subject={course.name} />

	{#if result}
		<StatStrip
			class="my-3"
			items={[
				{
					label: 'Điểm',
					value: num(result.score),
					note: `/${num(result.max)}, thang 10: ${num(result.max ? (Math.max(0, result.score) / result.max) * 10 : 0, 1)}`,
					tone: 'primary',
				},
				{ label: 'Đúng', value: result.right, tone: 'ok' },
				{ label: 'Sai', value: result.wrong, tone: result.wrong ? 'bad' : undefined },
				{ label: 'Bỏ trống', value: result.blank, note: clock(result.seconds) },
			]}
		/>
		{@const slow = shown.filter((q) => (times[q.id] ?? 0) > pace * 1.5)}
		{#if slow.length}
			<p class="mb-2 text-sm text-warn">
				{slow.length} câu làm quá 1,5 lần thời gian trung bình mỗi câu ({clock(Math.round(pace))}/câu): câu {slow
					.map((q) => shown.indexOf(q) + 1)
					.join(', ')}.
			</p>
		{/if}
		<Panel title="Xem lại" pad>
			{#each shown as q, k (q.id)}<QuestionCard
					question={q}
					index={k}
					mode="result"
					order={orders[q.id]}
					picked={picked[q.id] ?? null}
					seconds={times[q.id]}
					{pace}
				/>{/each}
		</Panel>
		<div class="mt-4 flex gap-2">
			<Button href={'#luyen-tap/' + course.id}>Về trang môn</Button>
			<Button variant="outline" href="#ket-qua">Xem lịch sử thi thử</Button>
		</div>
	{:else}
		<!-- Nền đặc (popover), không dùng bg-card: thẻ trong app trong suốt nên đề cuộn bên dưới bị lộ qua thanh này.
		     -top-3 bù padding p-3 của #content, để thanh dính sát mép trên, không hở dải nào. -->
		<div class="sticky -top-3 z-10 my-3 flex items-center gap-4 rounded-xl bg-popover px-4 py-2.5 shadow-md ring-1 ring-foreground/10">
			<span class="font-mono text-xl font-semibold tabular-nums {left <= 300 ? 'text-bad' : ''}">{clock(left)}</span>
			<span class="text-sm text-muted-foreground">{Object.keys(picked).length}/{qs.length} câu đã chọn</span>
			<span class="flex gap-1"><ShuffleToggles bind:prefs storageKey={EXAM_SHUFFLE} /></span>
			<Button class="ml-auto" variant={armed ? 'destructive' : 'default'} onclick={submit}
				>{armed ? 'Chọn lần nữa để nộp' : 'Nộp bài'}</Button
			>
		</div>
		<Panel pad>
			{#each shown as q, k (q.id)}
				<QuestionCard
					question={q}
					index={k}
					mode="exam"
					order={orders[q.id]}
					picked={picked[q.id] ?? null}
					onpick={(q, j) => pick(q.id, j)}
				/>
			{/each}
		</Panel>
	{/if}
{/if}
