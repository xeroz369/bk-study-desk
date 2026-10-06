<!-- Luyện trộn (#luyen-tron/<môn>): một lượt câu xen kẽ các chương (interleave.ts), chấm ngay như bài học.
     Câu trả lời ghi vào tiến độ và lịch ôn như mọi câu khác. -->
<script lang="ts">
	import { untrack } from 'svelte';
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import QuestionCard from '$lib/components/app/QuestionCard.svelte';
	import EmptyRow from '$lib/components/app/EmptyRow.svelte';
	import { Button } from '$lib/components/ui/button';
	import { Study } from '$lib/study/registry.svelte';
	import { Progress } from '$lib/study/progress.svelte';
	import { LABELS, mixQuestions } from '$lib/study/actions';

	let { arg }: { arg: string } = $props();
	const course = untrack(() => Study.course(arg));
	// Lượt câu cố định khi mở trang; "Lượt mới" rút lại (câu vừa làm xuống sau vì không còn "chưa làm").
	let questions = $state(untrack(() => mixQuestions(arg)));
	let answered = $state<Record<string, true>>({});
	const done = $derived(Object.keys(answered).length);

	function next() {
		questions = mixQuestions(arg);
		answered = {};
		scrollTo(0, 0);
	}
</script>

<PageShell
	title={`${LABELS.mix}${course ? `, ${course.name}` : ''}`}
	crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }, ...(course ? [{ label: course.name, href: '#luyen-tap/' + course.id }] : [])]}
	meta={questions.length ? `${done}/${questions.length} câu, xen kẽ các chương` : ''}
>
	{#snippet actions()}{#if questions.length}<Button size="sm" variant="outline" onclick={next}>Lượt mới</Button>{/if}{/snippet}
</PageShell>

<Panel pad>
	{#if questions.length}
		<p class="text-xs text-muted-foreground">
			Câu các chương đan xen nên khó hơn làm từng chương: phải tự nhận ra dạng bài trước khi giải. Sai nhiều hơn lúc luyện là bình
			thường, đổi lại nhớ lâu và chọn đúng cách giải khi thi.
		</p>
	{/if}
	{#each questions as q, k (q.id)}
		{@const e = Study.entry(q.lessonId ?? '')}
		{#if e}<a class="link mt-2 flex min-h-6 items-center text-xs text-muted-foreground" href={'#bai/' + e.id}>{e.title}</a>{/if}
		<QuestionCard
			question={q}
			index={k}
			mode="practice"
			onanswer={(q, j, ok) => {
				answered[q.id] = true;
				Progress.recordAnswer(q.id, j, ok);
			}}
		/>
	{:else}
		<EmptyRow>Môn này cần câu hỏi ở ít nhất hai chương để luyện trộn. Tạo hoặc nhập câu hỏi trước.</EmptyRow>
	{/each}
</Panel>
