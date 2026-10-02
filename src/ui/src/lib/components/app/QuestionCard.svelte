<!-- Một câu hỏi, đủ loại như quiz LMS: một đáp án, nhiều đáp án, điền số, điền chữ (Đúng/Sai là một đáp án 2 phương án).
     practice: báo đúng sai ngay, sai thì làm lại, đúng thì hiện lời giải.
     exam: chỉ chọn/gõ (bấm lại để bỏ trống), không báo đúng sai.
     result: xem lại sau khi nộp, tô đáp án đúng và đáp án đã chọn.
     Dưới mỗi câu: mở slide đúng trang, ghi chú riêng, cờ "nghi đáp án sai" (lưu theo fingerprint). -->
<script lang="ts">
	import type { Answer, Question } from '$lib/study/types';
	import { Progress } from '$lib/study/progress.svelte';
	import { answerText, isBlank, isCorrect, qType } from '$lib/study/grade';
	import { typeset } from '$lib/math';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import { Study } from '$lib/study/registry.svelte';
	import { api } from '$lib/api/client';
	import { clock } from '$lib/format';
	import FileText from '@lucide/svelte/icons/file-text';
	import Flag from '@lucide/svelte/icons/flag';
	import StickyNote from '@lucide/svelte/icons/sticky-note';

	let {
		question: q,
		index,
		mode,
		picked = null,
		order,
		onanswer,
		onpick,
		seconds,
		pace,
	}: {
		question: Question;
		index: number;
		mode: 'practice' | 'exam' | 'result';
		picked?: Answer | null;
		/** display order of options (position -> original index); answers stay in original indices */
		order?: number[];
		onanswer?: (q: Question, chosen: Answer, correct: boolean) => void;
		onpick?: (q: Question, chosen: Answer | null) => void;
		/** result: seconds spent on this question; pace: seconds per question in the real exam */
		seconds?: number;
		pace?: number;
	} = $props();

	const L = 'ABCDEFGH';
	const shownOrder = $derived(order ?? q.options.map((_, i) => i));
	const reordered = $derived(shownOrder.some((v, i) => v !== i));
	/** letter of an ORIGINAL option index as the learner sees it */
	const letter = (i: number) => L[shownOrder.indexOf(i)] ?? '?';
	const type = $derived(qType(q));
	const rightText = $derived(
		type === 'multi' ? (q.answers ?? []).map(letter).sort().join(', ') : type === 'single' ? letter(q.answer) : answerText(q),
	);
	let tried = $state<Answer | null>(null); // lần trả lời gần nhất (luyện tập)
	let sel = $state<number[]>([]); // multi: đang tick (luyện tập)
	let text = $state(''); // numeric/short: đang gõ (luyện tập)
	const rec = $derived(Progress.q(q.id));
	const solved = $derived(tried !== null && isCorrect(q, tried));

	// Practice timer: from the moment the card is on screen to the first answer.
	let shownAt = 0;
	function watch(el: HTMLElement) {
		if (mode !== 'practice') return;
		const io = new IntersectionObserver(
			([e]) => {
				if (e.isIntersecting && !shownAt) shownAt = Date.now();
			},
			{ threshold: 0.5 },
		);
		io.observe(el);
		return () => io.disconnect();
	}

	function submit(a: Answer) {
		if (isBlank(a)) return;
		if (tried === null && shownAt) Progress.recordTime(q.fp, (Date.now() - shownAt) / 1000);
		tried = Array.isArray(a) ? [...a] : a;
		onanswer?.(q, tried, isCorrect(q, tried));
	}
	function choose(j: number) {
		if (type === 'multi') {
			if (mode === 'practice') sel = sel.includes(j) ? sel.filter((x) => x !== j) : [...sel, j];
			else if (mode === 'exam') {
				const cur = Array.isArray(picked) ? picked : [];
				const next = cur.includes(j) ? cur.filter((x) => x !== j) : [...cur, j];
				onpick?.(q, next.length ? next : null);
			}
			return;
		}
		if (mode === 'practice') submit(j);
		else if (mode === 'exam') onpick?.(q, picked === j ? null : j);
	}

	const isPicked = (j: number) =>
		type === 'multi'
			? mode === 'practice'
				? sel.includes(j)
				: Array.isArray(picked) && picked.includes(j)
			: mode === 'practice'
				? tried === j
				: picked === j;
	const isRight = (j: number) => (type === 'multi' ? (q.answers ?? []).includes(j) : j === q.answer);

	function optClass(j: number) {
		const base = 'flex w-full items-start gap-2 rounded-lg border px-3 py-2 text-left text-[15px] transition-colors';
		if (mode === 'result') {
			if (isRight(j)) return `${base} border-ok/50 bg-ok-bg`;
			if (isPicked(j)) return `${base} border-bad/50 bg-bad-bg`;
			return `${base} opacity-70`;
		}
		if (mode === 'exam' || (type === 'multi' && tried === null))
			return `${base} hover:bg-muted ${isPicked(j) ? 'border-primary bg-accent' : ''}`;
		if (type === 'multi' && tried !== null)
			return `${base} ${isPicked(j) ? (isRight(j) ? 'border-ok/50 bg-ok-bg' : 'border-bad/50 bg-bad-bg') : 'hover:bg-muted'}`;
		if (tried === j) return `${base} ${isRight(j) ? 'border-ok/50 bg-ok-bg' : 'border-bad/50 bg-bad-bg'}`;
		return `${base} hover:bg-muted`;
	}

	const status = $derived.by(() => {
		if (mode === 'practice' && rec) return rec.correct ? 'đã làm đúng' : 'đã làm, chưa đúng';
		if (mode === 'result') return isBlank(picked) ? 'bỏ trống' : isCorrect(q, picked) ? 'đúng' : 'sai';
		return '';
	});
	// Slide: tag "tr.12" wins, else the first page of the lesson's first page-numbered source.
	const slide = $derived.by(() => {
		const lid = q.lessonId;
		const entry = lid ? Study.entry(lid) : undefined;
		const src = (Study.lessons[lid ?? '']?.sources ?? entry?.sources ?? []).find((s) => /^\d/.test(s.pages ?? ''));
		if (!entry || !src) return null;
		const page = Number(/tr\.?\s*(\d+)/i.exec(q.tag ?? '')?.[1] ?? /^\d+/.exec(src.pages ?? '')?.[0] ?? 0);
		return { subject: entry.courseName, file: src.file, page };
	});
	let slideMissing = $state(false);
	async function openSlide() {
		if (slide) slideMissing = !(await api.openSource(slide.subject, slide.file, slide.page).catch(() => null));
	}

	const note = $derived(Progress.note(q.fp));
	let editing = $state(false);
	let draft = $state('');
	function editNote() {
		draft = note?.text ?? '';
		editing = true;
	}
	function saveNote() {
		Progress.setNote(q.fp, { text: draft.trim() || undefined });
		editing = false;
	}
	const slow = $derived(!!seconds && !!pace && seconds > pace * 1.5);

	const hint = $derived(
		type === 'multi'
			? 'chọn tất cả phương án đúng'
			: type === 'numeric'
				? `gõ đáp số${q.unit ? ` (${q.unit})` : ''}`
				: type === 'short'
					? 'gõ câu trả lời'
					: '',
	);
</script>

<div
	class="question flex flex-col gap-2.5 border-t py-4 first:border-t-0 first:pt-0"
	use:typeset={`${q.id}|${solved}|${mode}|${editing}`}
	{@attach watch}
>
	<div class="flex items-center gap-2 text-xs text-muted-foreground">
		<!-- Nhãn nguồn (ví dụ "GK251 câu 3"); bỏ khi chỉ lặp lại số câu. -->
		{#if q.tag && q.tag.trim() !== `Câu ${index + 1}`}<span class="rounded bg-muted px-1.5 py-0.5 font-medium">{q.tag}</span>{/if}
		<span class="font-medium text-foreground">Câu {index + 1}</span>
		{#if hint}<span>· {hint}</span>{/if}
		{#if status}<span>· {status}</span>{/if}
		{#if mode === 'result' && seconds}<span class={slow ? 'font-medium text-warn' : ''}
				>· {clock(seconds)}{slow ? ` (chậm, TB ${clock(Math.round(pace!))})` : ''}</span
			>{/if}
		{#if note?.flag}<span class="font-medium text-warn">· nghi đáp án sai</span>{/if}
	</div>
	<div class="prose-lesson">{@html q.prompt}</div>

	{#if type === 'single' || type === 'multi'}
		<div class="grid gap-1.5 sm:grid-cols-2">
			{#each shownOrder as j, pos (j)}
				<button
					class={optClass(j)}
					disabled={mode === 'result'}
					onclick={() => choose(j)}
					aria-pressed={type === 'multi' ? isPicked(j) : undefined}
				>
					<span class="font-semibold text-muted-foreground">{type === 'multi' ? (isPicked(j) ? '☑' : '☐') : L[pos] + '.'}</span>
					<span class="min-w-0">{@html q.options[j]}</span>
				</button>
			{/each}
		</div>
		{#if type === 'multi' && mode === 'practice'}
			<Button size="sm" variant="outline" class="self-start" disabled={!sel.length} onclick={() => submit(sel)}>Kiểm tra</Button>
		{/if}
	{:else if mode === 'practice'}
		<form
			class="flex items-center gap-2"
			onsubmit={(e) => {
				e.preventDefault();
				submit(text);
			}}
		>
			<Input
				bind:value={text}
				class="max-w-64"
				inputmode={type === 'numeric' ? 'decimal' : 'text'}
				placeholder={type === 'numeric' ? 'ví dụ 0,1875' : 'câu trả lời'}
			/>
			{#if q.unit}<span class="text-sm text-muted-foreground">{q.unit}</span>{/if}
			<Button size="sm" variant="outline" type="submit" disabled={!text.trim()}>Kiểm tra</Button>
		</form>
	{:else if mode === 'exam'}
		<div class="flex items-center gap-2">
			<Input
				value={typeof picked === 'string' ? picked : ''}
				class="max-w-64"
				inputmode={type === 'numeric' ? 'decimal' : 'text'}
				oninput={(e) => onpick?.(q, e.currentTarget.value.trim() ? e.currentTarget.value : null)}
			/>
			{#if q.unit}<span class="text-sm text-muted-foreground">{q.unit}</span>{/if}
		</div>
	{:else}
		<p class="text-sm">
			Bạn trả lời: <b class={isCorrect(q, picked) ? 'text-ok' : 'text-bad'}>{isBlank(picked) ? '(trống)' : String(picked)}</b> · Đáp án:
			<b>{answerText(q)}</b>
		</p>
	{/if}

	{#if mode === 'practice' && tried !== null}
		<p class="text-sm font-medium {solved ? 'text-ok' : 'text-bad'}" aria-live="polite">
			{solved
				? 'Đúng.'
				: type === 'multi'
					? 'Chưa đúng: chọn thiếu hoặc thừa. Kiểm tra lại rồi thử lại.'
					: 'Chưa đúng. Kiểm tra lại từng bước rồi làm lại.'}
		</p>
	{/if}
	{#if solved || mode === 'result'}
		{#if (mode === 'practice' && type !== 'single') || reordered}<p class="text-sm">
				Đáp án: <b>{rightText}</b>{#if reordered}<span class="text-xs text-muted-foreground"
						>&nbsp;· phương án đã xáo, chữ cái trong lời giải theo thứ tự gốc</span
					>{/if}
			</p>{/if}
		<div class="prose-lesson rounded-lg bg-muted/70 px-3 py-2 text-sm">{@html q.solution}</div>
	{/if}

	{#if mode !== 'exam'}
		{#if note?.text && !editing}
			<p class="rounded-lg border border-dashed px-3 py-1.5 text-sm whitespace-pre-wrap">
				<b class="text-xs text-muted-foreground">Ghi chú:</b>
				{note.text}
			</p>
		{/if}
		{#if editing}
			<Textarea bind:value={draft} rows={2} class="text-sm" placeholder="Ghi chú cho riêng bạn: chỗ hay nhầm, cách bấm máy…" />
			<div class="flex gap-1.5">
				<Button size="xs" onclick={saveNote}>Lưu ghi chú</Button>
				<Button size="xs" variant="ghost" onclick={() => (editing = false)}>Hủy</Button>
			</div>
		{:else}
			<div class="-ml-2 flex flex-wrap items-center gap-0.5">
				{#if slide}
					<Button size="xs" variant="ghost" title={slide.file} onclick={openSlide}
						><FileText />Mở slide{slide.page ? ` tr.${slide.page}` : ''}</Button
					>
					{#if slideMissing}<span class="text-xs text-bad">không thấy file trong Môn học\{slide.subject}</span>{/if}
				{/if}
				<Button size="xs" variant="ghost" onclick={editNote}><StickyNote />{note?.text ? 'Sửa ghi chú' : 'Ghi chú'}</Button>
				<Button
					size="xs"
					variant="ghost"
					class={note?.flag ? 'text-warn' : ''}
					aria-pressed={!!note?.flag}
					title="Đánh dấu khi nghĩ đáp án/lời giải sai; xem lại ở trang Đánh dấu"
					onclick={() => Progress.setNote(q.fp, { flag: !note?.flag })}><Flag />{note?.flag ? 'Bỏ cờ nghi sai' : 'Nghi đáp án sai'}</Button
				>
			</div>
		{/if}
	{/if}
</div>
