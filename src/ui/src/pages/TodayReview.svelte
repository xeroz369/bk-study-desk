<!-- Ôn hôm nay: lặp lại ngắt quãng kiểu Leitner. Sai -> hộp 1 (mai ôn lại), đúng -> lên hộp, giãn 1·3·7·14·30 ngày.
     Chỉ lần trả lời đầu tiên trong ngày làm câu đổi hộp. Lọc theo môn (#on-hom-nay/<môn>). -->
<script lang="ts">
	import { untrack } from 'svelte';
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import QuestionCard from '$lib/components/app/QuestionCard.svelte';
	import EmptyRow from '$lib/components/app/EmptyRow.svelte';
	import { Study } from '$lib/study/registry.svelte';
	import { Progress } from '$lib/study/progress.svelte';

	let { arg }: { arg: string } = $props();
	// List is fixed when the page opens; answered questions move to a later day on the next visit.
	const questions = untrack(() => Progress.dueQuestions(arg || undefined));
	// One batch of 40 per visit; the rest waits for the next visit (same count as the Practice button).
	const total = untrack(() => Progress.dueQuestions(arg || undefined, 999).length);
	const tabs: [string, string][] = [['', 'Tất cả'], ...Study.manifest.courses.map((c) => [c.id, c.name] as [string, string])];
	let answered = $state<Record<string, true>>({});
	const done = $derived(Object.keys(answered).length);
</script>

<PageShell
	title="Ôn hôm nay"
	crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }]}
	{tabs}
	tab={arg}
	base="on-hom-nay"
	meta={questions.length
		? `${done}/${questions.length} câu${total > questions.length ? ` (còn ${total - questions.length} câu ở lượt sau)` : ''} · câu hay sai lên trước`
		: 'không có câu tới hạn'}
/>

<Panel pad>
	{#each questions as q, k (q.id)}
		{@const e = Study.entry(q.lessonId ?? '')}
		{@const r = q.fp ? Progress.state.srs[q.fp] : undefined}
		{#if e}<a class="link mt-2 flex min-h-6 items-center text-xs text-muted-foreground" href={'#bai/' + e.id}
				>{e.courseName} · {e.title}{r ? ` · hộp ${r.box}, sai ${r.wrong}/${r.seen} lần` : ''}</a
			>{/if}
		<QuestionCard
			question={q}
			index={k}
			mode="practice"
			onanswer={(q, j, ok) => {
				answered[q.id] = true;
				Progress.recordAnswer(q.id, j, ok);
				if (ok) Progress.markReviewed(q.id, ok);
			}}
		/>
	{:else}
		<EmptyRow
			>Hôm nay không có câu tới hạn. Câu bạn làm trong bài học và đề thi thử sẽ được hẹn ôn lại: sai thì mai gặp lại, đúng thì giãn dần 1,
			3, 7, 14, 30 ngày.</EmptyRow
		>
	{/each}
</Panel>
