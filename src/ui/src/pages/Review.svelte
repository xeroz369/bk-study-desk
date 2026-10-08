<!-- Ôn câu sai: câu sai ở lần đầu, chưa làm lại đúng. Lọc theo môn (#on-cau-sai/<môn>).
     Danh sách chốt khi mở trang: làm lại đúng thì câu rời danh sách ở lần mở sau. -->
<script lang="ts">
	import { untrack } from 'svelte';
	import PageShell from '$lib/components/app/PageShell.svelte';
	import Panel from '$lib/components/app/Panel.svelte';
	import QuestionCard from '$lib/components/app/QuestionCard.svelte';
	import EmptyRow from '$lib/components/app/EmptyRow.svelte';
	import { Study } from '$lib/study/registry.svelte';
	import { Progress } from '$lib/study/progress.svelte';

	let { arg }: { arg: string } = $props();
	const questions = untrack(() =>
		Study.lessonQuestions()
			.filter((q) => Progress.needsReview(q.id))
			.filter((q) => !arg || Study.entry(q.lessonId ?? '')?.courseId === arg),
	);
	const tabs: [string, string][] = [['', 'Tất cả'], ...Study.manifest.courses.map((c) => [c.id, c.name] as [string, string])];
</script>

<PageShell
	title="Ôn câu sai"
	crumbs={[{ label: 'Luyện tập', href: '#luyen-tap' }]}
	{tabs}
	tab={arg}
	base="on-cau-sai"
	meta={`${questions.length} câu sai lần đầu, chưa làm lại đúng`}
/>

<Panel pad>
	{#each questions as q, k (q.id)}
		{@const e = Study.entry(q.lessonId ?? '')}
		{#if e}<a class="link mt-2 flex min-h-6 items-center text-xs text-muted-foreground" href={'#bai/' + e.id}
				>{e.courseName}, {e.title}</a
			>{/if}
		<QuestionCard
			question={q}
			index={k}
			mode="practice"
			onanswer={(q, j, ok) => {
				Progress.recordAnswer(q.id, j, ok);
				Progress.markReviewed(q.id, ok);
			}}
		/>
	{:else}
		<EmptyRow>Không còn câu sai cần ôn.</EmptyRow>
	{/each}
</Panel>
